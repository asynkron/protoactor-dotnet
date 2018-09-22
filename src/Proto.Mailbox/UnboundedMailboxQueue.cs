// -----------------------------------------------------------------------
//  <copyright file="UnboundedMailboxQueue.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;

namespace Proto.Mailbox
{
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

        public bool HasMessages => !_messages.IsEmpty;
    }
}