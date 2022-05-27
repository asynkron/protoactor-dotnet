// -----------------------------------------------------------------------
// <copyright file = "LockingUnboundedMailboxQueue.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;

namespace Proto.Mailbox;

public class LockingUnboundedMailboxQueue : IMailboxQueue
{
    private readonly object _lock = new();
    private readonly Queue<object> _queue;

    public LockingUnboundedMailboxQueue(int initialCapacity)
    {
        _queue = new Queue<object>(initialCapacity);
    }

    public bool HasMessages {
        get {
            lock (_lock)
            {
                return _queue.Count > 0;
            }
        }
    }
    public int Length {
        get {
            lock (_lock)
            {
                return _queue.Count;
            }
        }
    }

    public void Push(object message)
    {
        lock (_lock)
        {
            _queue.Enqueue(message);
        }
    }

    public object? Pop()
    {
        lock (_lock)
        {
            _queue.TryDequeue(out var msg);
            return msg;
        }
    }
}