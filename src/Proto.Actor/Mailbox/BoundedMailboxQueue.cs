// -----------------------------------------------------------------------
// <copyright file="BoundedMailboxQueue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Channels;

namespace Proto.Mailbox
{
    public class BoundedMailboxQueue : IMailboxQueue
    {
        private readonly Channel<object> _messages;
        private volatile bool _hasMessages;
        private long _length;

        public BoundedMailboxQueue(int size) => _messages = Channel.CreateBounded<object>(size);

        public int Length => (int)Interlocked.Read(ref _length);

        public void Push(object message)
        {
            while (!_messages.Writer.TryWrite(message))
            {
                Thread.Sleep(50);
            }

            Interlocked.Increment(ref _length);
        }

        public object? Pop()
        {
            if (_messages.Reader.TryRead(out var message))
            {
                Interlocked.Decrement(ref _length);
                _hasMessages = true;
            }
            else _hasMessages = false;

            return message;
        }

        public bool HasMessages => _hasMessages;
    }
}