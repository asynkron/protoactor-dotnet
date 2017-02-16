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

    public class DefaultMailbox : IMailbox
    {
        private readonly IMailboxStatistics[] _stats;
        private readonly IMailboxQueue _systemMessages;
        private readonly IMailboxQueue _userMailbox;
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public DefaultMailbox(IMailboxQueue systemMessages, IMailboxQueue userMailbox, params IMailboxStatistics[] stats)
        {
            _systemMessages = systemMessages;
            _userMailbox = userMailbox;
            _stats = stats ?? Array.Empty<IMailboxStatistics>();
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

        private Task RunAsync()
        {
            var done = ProcessMessages();

            if (!done)
                // mailbox is halted, awaiting completion of a message task, upon which mailbox will be rescheduled
                return Task.CompletedTask;

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || !_suspended && _userMailbox.HasMessages)
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
            return Task.CompletedTask;
        }

        private bool ProcessMessages()
        {
            for (var i = 0; i < _dispatcher.Throughput; i++)
            {
                object msg;
                if ((msg = _systemMessages.Pop()) != null)
                {
                    if (msg is SuspendMailbox)
                    {
                        _suspended = true;
                    }
                    if (msg is ResumeMailbox)
                    {
                        _suspended = false;
                    }
                    var t = _invoker.InvokeSystemMessageAsync(msg);
                    if (t.IsFaulted)
                    {
                        _invoker.EscalateFailure(t.Exception, msg);
                        continue;
                    }
                    if (!t.IsCompleted)
                    {
                        // if task didn't complete immediately, halt processing and reschedule a new run when task completes
                        t.ContinueWith(RescheduleOnTaskComplete, msg);
                        return false;
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
                        _invoker.EscalateFailure(t.Exception, msg);
                        continue;
                    }
                    if (!t.IsCompleted)
                    {
                        // if task didn't complete immediately, halt processing and reschedule a new run when task completes
                        t.ContinueWith(RescheduleOnTaskComplete, msg);
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
            return true;
        }

        private void RescheduleOnTaskComplete(Task task, object message)
        {
            if (task.IsFaulted)
            {
                _invoker.EscalateFailure(task.Exception, message);
            }
            else
            {
                for (var si = 0; si < _stats.Length; si++)
                {
                    _stats[si].MessageReceived(message);
                }
            }
            ProcessMessages();
        }


        protected void Schedule()
        {
            if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }

    public interface IMailboxStatistics
    {
        void MailboxStarted();
        void MessagePosted(object message);
        void MessageReceived(object message);
        void MailboxEmpty();
    }
}