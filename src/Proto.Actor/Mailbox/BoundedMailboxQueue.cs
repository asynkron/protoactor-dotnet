// -----------------------------------------------------------------------
//  <copyright file="BoundedMailboxQueue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox
{
    internal class BoundedMailboxQueue : IMailboxQueue
    {
        private readonly MPMCQueue _messages;

        public BoundedMailboxQueue(int size)
        {
            _messages = new MPMCQueue(size);
        }

        public void Push(object message) => _messages.Enqueue(message);

        public object? Pop()
            => _messages.TryDequeue(out var message)
                ? message
                : null;

        public bool HasMessages => _messages.Count > 0;
    }
}