// -----------------------------------------------------------------------
//  <copyright file="Queue.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox
{
    public interface IMailboxQueue
    {
        bool HasMessages { get; }
        void Push(object message);
        object Pop();
    }
}