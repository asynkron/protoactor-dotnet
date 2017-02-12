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
            //we follow the Go model for consistency.
            process:
            var done = ProcessMessages();

            if (!done)
                return Task.FromResult(0);

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_systemMessages.HasMessages || !_suspended && _userMailbox.HasMessages)
            {
                if (Interlocked.CompareExchange(ref _status, MailboxStatus.Busy, MailboxStatus.Idle) ==
                    MailboxStatus.Idle)
                {
                    goto process;
                }
            }
            else
            {
                for (var i = 0; i < _stats.Length; i++)
                {
                    _stats[i].MailboxEmpty();
                }
            }
            return Task.FromResult(0);
        }

        //TODO: we can gain a good 10% perf by not having async here.
        //but then we need some way to deal with non completed tasks, and handle mailbox idle/busy state for those
        private bool ProcessMessages()
        {
            var t = _dispatcher.Throughput;
            object message = null;
            try
            {
                for (var i = 0; i < t; i++)
                {
                    var sys = _systemMessages.Pop();
                    message = sys;
                    if (sys != null)
                    {
                        if (sys is SuspendMailbox)
                        {
                            _suspended = true;
                        }
                        if (sys is ResumeMailbox)
                        {
                            _suspended = false;
                        }
                        var t1 = _invoker.InvokeSystemMessageAsync(sys);
                        if (t1.IsCompleted)
                            continue;
                        else
                        {
                            t1.ContinueWith(RescheduleOnTaskComplete, message);
                            return false;
                        }
                    }
                    if (_suspended)
                    {
                        break;
                    }
                    var msg = _userMailbox.Pop();
                    if (msg != null)
                    {
                        message = msg;
                        var t1 = _invoker.InvokeUserMessageAsync(msg);
                        if (t1.IsCompleted)
                        {
                            for (var si = 0; si < _stats.Length; si++)
                            {
                                _stats[si].MessageReceived(msg);
                            }
                        }
                        else
                        {
                            t1.ContinueWith(RescheduleOnTaskComplete, message);
                            return false;
                        }
                    }
                    else
                    {
                        break;
                    }
                }
            }
            catch (Exception x)
            {
                _invoker.EscalateFailure(x, message);
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