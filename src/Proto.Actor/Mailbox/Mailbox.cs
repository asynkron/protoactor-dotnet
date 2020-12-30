// -----------------------------------------------------------------------
// <copyright file="Mailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    static class MailboxStatus
    {
        public const int Idle = 0;
        public const int Busy = 1;
    }

    public interface IMailbox
    {
        void PostUserMessage(object msg);

        void PostSystemMessage(object msg);

        void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher);

        void Start();
    }

    public static class BoundedMailbox
    {
        public static IMailbox Create(int size, params IMailboxStatistics[] stats) =>
            new DefaultMailbox(new UnboundedMailboxQueue(), new BoundedMailboxQueue(size), stats);
    }

    public static class UnboundedMailbox
    {
        public static IMailbox Create(params IMailboxStatistics[] stats) =>
            new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue(), stats);
    }

    public class DefaultMailbox : IMailbox
    {
        private readonly IMailboxStatistics[] _stats;
        private readonly IMailboxQueue _systemMessages;
        private readonly IMailboxQueue _userMailbox;
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;
        private long _systemMessageCount;

        public DefaultMailbox(
            IMailboxQueue systemMessages,
            IMailboxQueue userMailbox,
            params IMailboxStatistics[] stats
        )
        {
            _systemMessages = systemMessages;
            _userMailbox = userMailbox;
            _stats = stats ?? new IMailboxStatistics[0];

            _dispatcher = NoopDispatcher.Instance;
            _invoker = NoopInvoker.Instance;
        }

        public int Status => _status;

        public void PostUserMessage(object msg)
        {
            _userMailbox.Push(msg);

            foreach (var t in _stats)
            {
                t.MessagePosted(msg);
            }

            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);
            if (msg is Stop)
                _invoker?.CancellationTokenSource?.Cancel();
            Interlocked.Increment(ref _systemMessageCount);

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

        private Task RunAsync()
        {
            var done = ProcessMessages();

            if (!done)
                // mailbox is halted, awaiting completion of a message task, upon which mailbox will be rescheduled
                return Task.CompletedTask;

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || !_suspended && _userMailbox.HasMessages)
                Schedule();
            else
            {
                foreach (var t in _stats)
                {
                    t.MailboxEmpty();
                }
            }

            return Task.CompletedTask;
        }

        private bool ProcessMessages()
        {
            object? msg = null;

            try
            {
                for (var i = 0; i < _dispatcher.Throughput; i++)
                {
                    if (Interlocked.Read(ref _systemMessageCount) > 0 && (msg = _systemMessages.Pop()) is not null)
                    {
                        Interlocked.Decrement(ref _systemMessageCount);

                        _suspended = msg switch
                        {
                            SuspendMailbox _ => true,
                            ResumeMailbox _  => false,
                            _                => _suspended
                        };
                        var t = _invoker.InvokeSystemMessageAsync(msg);

                        if (t.IsFaulted)
                        {
                            _invoker.EscalateFailure(t.Exception, msg);
                            continue;
                        }

                        if (t.IsCanceled)
                        {
                            _invoker.EscalateFailure(new TaskCanceledException(), msg);
                            continue;
                        }

                        if (!t.IsCompleted)
                        {
                            // if task didn't complete immediately, halt processing and reschedule a new run when task completes
                            t.ContinueWith(RescheduleOnTaskComplete, msg);
                            return false;
                        }

                        foreach (var t1 in _stats)
                        {
                            t1.MessageReceived(msg);
                        }

                        continue;
                    }

                    if (_suspended) break;

                    if ((msg = _userMailbox.Pop()) is not null)
                    {
                        var t = _invoker.InvokeUserMessageAsync(msg);

                        if (t.IsFaulted)
                        {
                            _invoker.EscalateFailure(t.Exception, msg);
                            continue;
                        }

                        if (t.IsCanceled)
                        {
                            _invoker.EscalateFailure(new TaskCanceledException(), msg);
                            continue;
                        }

                        if (!t.IsCompleted)
                        {
                            // if task didn't complete immediately, halt processing and reschedule a new run when task completes
                            t.ContinueWith(RescheduleOnTaskComplete, msg);
                            return false;
                        }

                        foreach (var t1 in _stats)
                        {
                            t1.MessageReceived(msg);
                        }
                    }
                    else
                        break;
                }
            }
            catch (Exception e)
            {
                _invoker.EscalateFailure(e, msg);
            }

            return true;
        }

        private void RescheduleOnTaskComplete(Task task, object message)
        {
            if (task.IsFaulted)
                _invoker.EscalateFailure(task.Exception, message);
            else if (task.IsCanceled)
                _invoker.EscalateFailure(new TaskCanceledException(), message);
            else
            {
                foreach (var t in _stats)
                {
                    t.MessageReceived(message);
                }
            }

            _dispatcher.Schedule(RunAsync);
        }

        protected void Schedule()
        {
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
                _dispatcher.Schedule(RunAsync);
        }
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
}