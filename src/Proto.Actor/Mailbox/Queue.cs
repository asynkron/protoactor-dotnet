// -----------------------------------------------------------------------
// <copyright file="Queue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Mailbox
{
    public interface IMailboxQueue
    {
        bool HasMessages { get; }

        void Push(object message);

        object? Pop();
    }
}