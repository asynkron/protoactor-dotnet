// -----------------------------------------------------------------------
// <copyright file="BoundedMailboxQueue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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

        public BoundedMailboxQueue(int size) => _messages = Channel.CreateBounded<object>(size);

        public void Push(object message)
        {
            while (!_messages.Writer.TryWrite(message))
            {
                Thread.Sleep(50);
            }

            _hasMessages = true;
        }

        public object? Pop()
        {
            _hasMessages = _messages.Reader.TryRead(out var message);
            return message;
        }

        public bool HasMessages => _hasMessages;
    }
}