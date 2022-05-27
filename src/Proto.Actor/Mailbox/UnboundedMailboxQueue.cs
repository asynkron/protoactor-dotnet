﻿// -----------------------------------------------------------------------
// <copyright file="UnboundedMailboxQueue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;

namespace Proto.Mailbox;

public class UnboundedMailboxQueue : IMailboxQueue
{
    private readonly ConcurrentQueue<object> _messages = new();

    public int Length => _messages.Count;

    public void Push(object message) => _messages.Enqueue(message);

    public object? Pop() => _messages.TryDequeue(out var message) ? message : null;

    public bool HasMessages => !_messages.IsEmpty;
}