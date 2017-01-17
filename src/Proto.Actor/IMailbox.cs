// -----------------------------------------------------------------------
//  <copyright file="IMailbox.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Proto
{
    internal static class MailboxStatus
    {
        public const int Idle = 0;
        public const int Busy = 1;
    }

    public interface IMailbox
    {
        void PostUserMessage(object msg);
        void PostSystemMessage(SystemMessage sys);
        void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher);
    }

    public interface IMailboxQueue
    {
        bool HasMessages { get; }
        void Push(object message);
        object Pop();
    }

    public class BoundedMailboxQueue : IMailboxQueue
    {
        private readonly MPMCQueue _messages;

        public BoundedMailboxQueue(int size)
        {
            _messages = new MPMCQueue(size);
        }

        public void Push(object message)
        {
            _messages.Enqueue(message);
        }

        public object Pop()
        {
            object message;
            return _messages.TryDequeue(out message)
                ? message
                : null;
        }

        public bool HasMessages => _messages.Count > 0;
    }

    public class UnboundedMailboxQueue : IMailboxQueue
    {
        private readonly ConcurrentQueue<object> _messages = new ConcurrentQueue<object>();

        public void Push(object message)
        {
            _messages.Enqueue(message);
        }

        public object Pop()
        {
            object message;
            return _messages.TryDequeue(out message) ? message : null;
        }

        public bool HasMessages => _messages.Count > 0;
    }

    public class DefaultMailbox : IMailbox
    {
        private readonly IMailboxQueue _systemMessages;
        private readonly IMailboxQueue _userMailbox;
        private IDispatcher _dispatcher;
        private IMessageInvoker _invoker;

        private int _status = MailboxStatus.Idle;
        private bool _suspended;

        public DefaultMailbox(IMailboxQueue systemMessages, IMailboxQueue userMailbox)
        {
            _systemMessages = systemMessages;
            _userMailbox = userMailbox;
        }

        public void PostUserMessage(object msg)
        {
            _userMailbox.Push(msg);
            Schedule();
        }

        public void PostSystemMessage(SystemMessage sys)
        {
            _systemMessages.Push(sys);
            Schedule();
        }

        public void RegisterHandlers(IMessageInvoker invoker, IDispatcher dispatcher)
        {
            _invoker = invoker;
            _dispatcher = dispatcher;
        }

        private async Task RunAsync()
        {
            var t = _dispatcher.Throughput;

            for (var i = 0; i < t; i++)
            {
                var sys = (SystemMessage) _systemMessages.Pop();
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
                    _invoker.InvokeSystemMessage(sys);
                    continue;
                }
                if (_suspended)
                {
                    break;
                }
                var msg = _userMailbox.Pop();
                if (msg != null)
                {
                    await _invoker.InvokeUserMessageAsync(msg);
                }
                else
                {
                    break;
                }
            }

            Interlocked.Exchange(ref _status, MailboxStatus.Idle);

            if (_userMailbox.HasMessages || _systemMessages.HasMessages)
            {
                Schedule();
            }
        }

        protected void Schedule()
        {
            if (Interlocked.Exchange(ref _status, MailboxStatus.Busy) == MailboxStatus.Idle)
            {
                _dispatcher.Schedule(RunAsync);
            }
        }
    }
}