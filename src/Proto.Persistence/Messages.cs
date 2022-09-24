// -----------------------------------------------------------------------
// <copyright file="Messages.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence;

/// <summary>
///     Actor's persistent event
/// </summary>
public class Event
{
    public Event(object data, long index)
    {
        Data = data;
        Index = index;
    }

    /// <summary>
    ///     Event data
    /// </summary>
    public object Data { get; }

    /// <summary>
    ///     Event index
    /// </summary>
    public long Index { get; }
}

/// <summary>
///     Represents snapshot to be persisted
/// </summary>
public class PersistedSnapshot : Snapshot
{
    public PersistedSnapshot(object state, long index) : base(state, index)
    {
    }
}

/// <summary>
///     Represents snapshot that is being recovered
/// </summary>
public class RecoverSnapshot : Snapshot
{
    public RecoverSnapshot(object state, long index) : base(state, index)
    {
    }
}

/// <summary>
///     Wrapper for persistent snapshot
/// </summary>
public class Snapshot
{
    public Snapshot(object state, long index)
    {
        State = state;
        Index = index;
    }

    /// <summary>
    ///     Snapshot data
    /// </summary>
    public object State { get; }

    /// <summary>
    ///     Index of the last event included in the snapshot + 1
    /// </summary>
    public long Index { get; }
}

/// <summary>
///     Represents an event being recovered
/// </summary>
public class RecoverEvent : Event
{
    public RecoverEvent(object data, long index) : base(data, index)
    {
    }
}

/// <summary>
///     Represents an event being replayed
/// </summary>
public class ReplayEvent : Event
{
    public ReplayEvent(object data, long index) : base(data, index)
    {
    }
}

/// <summary>
///     Represents an event being stored
/// </summary>
public class PersistedEvent : Event
{
    public PersistedEvent(object data, long index) : base(data, index)
    {
    }
}