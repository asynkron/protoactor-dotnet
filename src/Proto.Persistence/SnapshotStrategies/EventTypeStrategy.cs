// -----------------------------------------------------------------------
// <copyright file="EventTypeStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Persistence.SnapshotStrategies;

/// <summary>
///     <see cref="ISnapshotStrategy" /> implementation that stores actor's state snapshot when an event of certain type is
///     written
/// </summary>
public class EventTypeStrategy : ISnapshotStrategy
{
    private readonly Type _eventType;

    public EventTypeStrategy(Type eventType)
    {
        _eventType = eventType;
    }

    public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => persistedEvent.Data.GetType() == _eventType;
}