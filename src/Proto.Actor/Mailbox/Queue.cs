// -----------------------------------------------------------------------
// <copyright file="Queue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Mailbox;

public interface IMailboxQueue
{
    bool HasMessages { get; }

    int Length { get; }

    void Push(object message);

    object? Pop();
}