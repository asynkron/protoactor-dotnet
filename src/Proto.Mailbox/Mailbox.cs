// -----------------------------------------------------------------------
//  <copyright file="Mailbox.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Mailbox
{
    internal static class MailboxStatus
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

    internal static class BoundedMailbox
    {
        public static IMailbox Create(int size, params IMailboxStatistics[] stats)
        {
            return new DefaultMailbox(new UnboundedMailboxQueue(), new BoundedMailboxQueue(size), stats);
        }
    }

    public static class UnboundedMailbox
    {
        public static IMailbox Create(params IMailboxStatistics[] stats)
        {
            return new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue(), stats);
        }
    }

    internal class DefaultMailbox : IMailbox
    {
        private readonly IMailboxStatistics[] _stats;
        private readonly IMailboxQueue _systemMessages;
        private readonly IMailboxQueue _userMailbox;
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        internal int Status => _status;

        public DefaultMailbox(IMailboxQueue systemMessages, IMailboxQueue userMailbox, params IMailboxStatistics[] stats)
        {
            _systemMessages = systemMessages;
            _userMailbox = userMailbox;
            _stats = stats ?? new IMailboxStatistics[0];
        }

        public void PostUserMessage(object msg)
        {
            _userMailbox.Push(msg);
            for (var i = 0; i < _stats.Length; i++)
            {
                _stats[i].MessagePosted(msg);
            }
            Schedule();
        }

        public void PostSystemMessage(object msg)
        {
            _systemMessages.Push(msg);
            for (var i = 0; i < _stats.Length; i++)
            {
                _stats[i].MessagePosted(msg);
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
            for (var i = 0; i < _stats.Length; i++)
            {
                _stats[i].MailboxStarted();
            }
        }

        private async Task RunAsync()
        {
            var done = await ProcessMessages();

            if (!done)
                // mailbox is halted, awaiting completion of a message task, upon which mailbox will be rescheduled
                return;

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || (!_suspended && _userMailbox.HasMessages))
            {
                Schedule();
            }
            else
            {
                for (var i = 0; i < _stats.Length; i++)
                {
                    _stats[i].MailboxEmpty();
                }
            }
        }

        private async Task<bool> ProcessMessages()
        {
            object msg = null;
            try
            {
                for (var i = 0; i < _dispatcher.Throughput; i++)
                {
                    if ((msg = _systemMessages.Pop()) != null)
                    {
                        if (msg is SuspendMailbox)
                        {
                            _suspended = true;
                        }
                        else if (msg is ResumeMailbox)
                        {
                            _suspended = false;
                        }
                        var t = _invoker.InvokeSystemMessageAsync(msg);
                        if (t.IsFaulted)
                        {
                            await _invoker.EscalateFailureAsync(t.Exception, msg);
                            continue;
                        }
                        if (!t.IsCompleted)
                        {
                            // if task didn't complete immediately, halt processing and reschedule a new run when task completes
#pragma warning disable 4014
                            t.ContinueWith(RescheduleOnTaskComplete, msg);
#pragma warning restore 4014
                            return false;
                        }
                        for (var si = 0; si < _stats.Length; si++)
                        {
                            _stats[si].MessageReceived(msg);
                        }
                        continue;
                    }
                    if (_suspended)
                    {
                        break;
                    }
                    if ((msg = _userMailbox.Pop()) != null)
                    {
                        var t = _invoker.InvokeUserMessageAsync(msg);
                        if (t.IsFaulted)
                        {
                            await _invoker.EscalateFailureAsync(t.Exception, msg);
                            continue;
                        }
                        if (!t.IsCompleted)
                        {
                            // if task didn't complete immediately, halt processing and reschedule a new run when task completes
#pragma warning disable 4014
                            t.ContinueWith(RescheduleOnTaskComplete, msg);
#pragma warning restore 4014
                            return false;
                        }
                        for (var si = 0; si < _stats.Length; si++)
                        {
                            _stats[si].MessageReceived(msg);
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
                await _invoker.EscalateFailureAsync(e, msg);
            }
            return true;
        }

        private async Task RescheduleOnTaskComplete(Task task, object message)
        {
            if (task.IsFaulted)
            {
                await _invoker.EscalateFailureAsync(task.Exception, message);
            }
            else
            {
                for (var si = 0; si < _stats.Length; si++)
                {
                    _stats[si].MessageReceived(message);
                }
            }
            _dispatcher.Schedule(RunAsync);
        }


        protected void Schedule()
        {
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }

    /// <summary>
    /// Extension point for getting notifications about mailbox events
    /// </summary>
    public interface IMailboxStatistics
    {
        /// <summary>
        /// This method is invoked when the mailbox is started
        /// </summary>
        void MailboxStarted();
        /// <summary>
        /// This method is invoked when a message is posted to the mailbox.
        /// </summary>
        void MessagePosted(object message);
        /// <summary>
        /// This method is invoked when a message has been received by the invoker associated with the mailbox.
        /// </summary>
        void MessageReceived(object message);
        /// <summary>
        /// This method is invoked when all messages in the mailbox have been received.
        /// </summary>
        void MailboxEmpty();
    }
}
