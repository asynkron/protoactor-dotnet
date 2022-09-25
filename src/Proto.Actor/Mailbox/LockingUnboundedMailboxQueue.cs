// -----------------------------------------------------------------------
// <copyright file = "LockingUnboundedMailboxQueue.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Threading;

namespace Proto.Mailbox;

public class LockingUnboundedMailboxQueue : IMailboxQueue
{
    private readonly object _lock = new();
    private readonly Queue<object> _queue;
    private long _count;

    public LockingUnboundedMailboxQueue(int initialCapacity)
    {
        _queue = new Queue<object>(initialCapacity);
    }

    public bool HasMessages => Length > 0;

    public int Length
    {
        get
        {
            Interlocked.Read(ref _count);

            return (int)_count;
        }
    }

    public void Push(object message)
    {
        lock (_lock)
        {
            _queue.Enqueue(message);
            Interlocked.Increment(ref _count);
        }
    }

    public object? Pop()
    {
        if (!HasMessages)
        {
            return null;
        }

        lock (_lock)
        {
            if (_queue.TryDequeue(out var msg))
            {
                Interlocked.Decrement(ref _count);
            }

            return msg;
        }
    }
}