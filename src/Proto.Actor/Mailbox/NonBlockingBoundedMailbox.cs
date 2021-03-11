// -----------------------------------------------------------------------
// <copyright file="NonBlockingBoundedMailbox.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Proto.Mailbox
{
    public class NonBlockingBoundedMailbox : IMailboxQueue
    {
        private readonly int _maxSize;

        private readonly ConcurrentQueue<object> _messages = new();
        private readonly Action<object> _overflowAction;
        private readonly TimeSpan _timeout;

        public NonBlockingBoundedMailbox(int maxSize, Action<object> overflowAction, TimeSpan timeout)
        {
            _maxSize = maxSize;
            _overflowAction = overflowAction;
            _timeout = timeout;
        }

        public int Length => _messages.Count;

        public void Push(object message)
        {
            if (SpinWait.SpinUntil(() => _messages.Count < _maxSize, _timeout))
            {
                //this will be racy, but best effort is good enough..
                _messages.Enqueue(message);
            }
            else
                _overflowAction(message);
        }

        public object? Pop() => _messages.TryDequeue(out var message) ? message : null;

        public bool HasMessages => !_messages.IsEmpty;
    }
}