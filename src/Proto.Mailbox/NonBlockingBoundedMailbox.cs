// -----------------------------------------------------------------------
//   <copyright file="NonBlockingBoundedMailbox.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proto.Mailbox
{
    public class NonBlockingBoundedMailbox : IMailboxQueue
    {
        public NonBlockingBoundedMailbox(int maxSize, Action<object> overflowAction, TimeSpan timeout)
        {
            _maxSize = maxSize;
            _overflowAction = overflowAction;
            _timeout = timeout;
        }

        private readonly ConcurrentQueue<object> _messages = new ConcurrentQueue<object>();
        private readonly int _maxSize;
        private readonly Action<object> _overflowAction;
        private readonly TimeSpan _timeout;

        public void Push(object message)
        {
            if (SpinWait.SpinUntil(() => _messages.Count < _maxSize, _timeout))
            {
                //this will be racy, but best effort is good enough..
                _messages.Enqueue(message);
            }
            else 
            {
                _overflowAction(message);
            }
        }

        public object Pop()
        {
            object message;
            return _messages.TryDequeue(out message) ? message : null;
        }

        public bool HasMessages => !_messages.IsEmpty;
    }
}
