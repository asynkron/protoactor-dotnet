// -----------------------------------------------------------------------
//  <copyright file="BoundedMailboxQueue.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
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

        public void Push(object message)
        {
            _messages.Enqueue(message);
        }

        public object Pop()
        {
            return _messages.TryDequeue(out var message)
                ? message
                : null;
        }

        public bool HasMessages => _messages.Count > 0;
    }
}