// -----------------------------------------------------------------------
// <copyright file="Mailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox;

public sealed class DefaultMailbox : IMailbox, IThreadPoolWorkItem

{
    private const int Idle = 0;
    private const int Busy = 1;

    private readonly IMailboxStatistics[] _stats;
    private readonly IMailboxQueue _systemMessages;
    private readonly IMailboxQueue _userMailbox;
    private IDispatcher _dispatcher;
    private IMessageInvoker _invoker;

    private long _status = Idle;
    private bool _suspended;

    public DefaultMailbox(
        IMailboxQueue systemMessages,
        IMailboxQueue userMailbox
    )
    {
        _systemMessages = systemMessages;
        _userMailbox = userMailbox;
        _stats = Array.Empty<IMailboxStatistics>();

        _dispatcher = NoopDispatcher.Instance;
        _invoker = NoopInvoker.Instance;
    }

    public DefaultMailbox(
        IMailboxQueue systemMessages,
        IMailboxQueue userMailbox,
        params IMailboxStatistics[] stats
    )
    {
        _systemMessages = systemMessages;
        _userMailbox = userMailbox;
        _stats = stats;

        _dispatcher = NoopDispatcher.Instance;
        _invoker = NoopInvoker.Instance;
    }

    public int Status => (int)Interlocked.Read(ref _status);

    public int UserMessageCount => _userMailbox.Length;

    public void PostUserMessage(object msg)
    {
        // if the message is a batch message, we unpack the content as individual messages in the mailbox
        // feature Aka: Samkuvertering in Swedish...
        if (msg is IMessageBatch || msg is MessageEnvelope { Message: IMessageBatch })
        {
            var batch = (IMessageBatch)MessageEnvelope.UnwrapMessage(msg)!;
            var messages = batch.GetMessages();

            foreach (var message in messages)
            {
                _userMailbox.Push(message);

                foreach (var t in _stats)
                {
                    t.MessagePosted(message);
                }
            }

            if (batch is IAutoRespond)
            {
                // push the batch itself as well, so that it can autorespond to sender after processing all contained messages
                // used by pub sub
                _userMailbox.Push(msg);
            }

            foreach (var t in _stats)
            {
                t.MessagePosted(msg);
            }

            Schedule();
        }
        else
        {
            _userMailbox.Push(msg);

            foreach (var t in _stats)
            {
                t.MessagePosted(msg);
            }

            Schedule();
        }
    }

    public void PostSystemMessage(object msg)
    {
        _systemMessages.Push(msg);

        if (msg is Stop)
        {
            _invoker?.CancellationTokenSource?.Cancel();
        }

        foreach (var t in _stats)
        {
            t.MessagePosted(msg);
        }

        Schedule();
    }

    public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
    {
        _invoker = invoker;
        _dispatcher = dispatcher;
    }

    public void Start()
    {
        foreach (var t in _stats)
        {
            t.MailboxStarted();
        }
    }

    private static Task RunAsync(DefaultMailbox mailbox)
    {
        var task = mailbox.ProcessMessages();

        if (!task.IsCompletedSuccessfully)
        {
            return Await(mailbox, task);
        }

        Interlocked.Exchange(ref mailbox._status, Idle);

        if (mailbox._systemMessages.HasMessages || mailbox is { _suspended: false, _userMailbox.HasMessages: true })
        {
            mailbox.Schedule();
        }
        else
        {
            foreach (var t in mailbox._stats)
            {
                t.MailboxEmpty();
            }
        }

        return Task.CompletedTask;

        static async Task Await(DefaultMailbox self, Task task)
        {
            await task.ConfigureAwait(false);

            Interlocked.Exchange(ref self._status, Idle);

            if (self._systemMessages.HasMessages || self is { _suspended: false, _userMailbox.HasMessages: true })
            {
                self.Schedule();
            }
            else
            {
                foreach (var t in self._stats)
                {
                    t.MailboxEmpty();
                }
            }
        }
    }

    private Task ProcessMessages()
    {
        object? msg = null;

        try
        {
            for (var i = 0; i < _dispatcher.Throughput; i++)
            {
                msg = _systemMessages.Pop();

                if (msg is SystemMessage sys)
                {
                    _suspended = msg switch
                    {
                        SuspendMailbox => true,
                        ResumeMailbox  => false,
                        _              => _suspended
                    };

                    var t = _invoker.InvokeSystemMessageAsync(sys);

                    if (!t.IsCompletedSuccessfully)
                    {
                        return Await(msg, t, this);
                    }

                    foreach (var t1 in _stats)
                    {
                        t1.MessageReceived(msg);
                    }

                    continue;
                }

                if (_suspended)
                {
                    break;
                }

                msg = _userMailbox.Pop();

                if (msg is not null)
                {
                    var t = _invoker.InvokeUserMessageAsync(msg);

                    if (!t.IsCompletedSuccessfully)
                    {
                        return Await(msg, t, this);
                    }

                    foreach (var t1 in _stats)
                    {
                        t1.MessageReceived(msg);
                    }
                }
                else
                {
                    break;
                }
            }
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            _invoker.EscalateFailure(e, msg);
        }

        return Task.CompletedTask;

        static async Task Await(object msg, Task task, DefaultMailbox self)
        {
            try
            {
                await task.ConfigureAwait(false);

                foreach (var t1 in self._stats)
                {
                    t1.MessageReceived(msg);
                }
            }
            catch (Exception e)
            {
                e.CheckFailFast();
                self._invoker.EscalateFailure(e, msg);
            }
        }
    }

    private void Schedule()
    {
        if (Interlocked.CompareExchange(ref _status, Busy, Idle) == Idle)
        {
            if (_dispatcher == Dispatchers.DefaultDispatcher)
            {
                ThreadPool.UnsafeQueueUserWorkItem(this, false);
            }
            else
            {
                _dispatcher.Schedule(() => RunAsync(this));
            }
        }
    }
    
    public void Execute() => _ = RunAsync(this);

}

/// <summary>
///     Extension point for getting notifications about mailbox events
/// </summary>
public interface IMailboxStatistics
{
    /// <summary>
    ///     This method is invoked when the mailbox is started
    /// </summary>
    void MailboxStarted();

    /// <summary>
    ///     This method is invoked when a message is posted to the mailbox.
    /// </summary>
    void MessagePosted(object message);

    /// <summary>
    ///     This method is invoked when a message has been received by the invoker associated with the mailbox.
    /// </summary>
    void MessageReceived(object message);

    /// <summary>
    ///     This method is invoked when all messages in the mailbox have been received.
    /// </summary>
    void MailboxEmpty();
}