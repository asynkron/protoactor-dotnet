// -----------------------------------------------------------------------
// <copyright file="IntervalStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence.SnapshotStrategies;

/// <summary>
///     <see cref="ISnapshotStrategy" /> implementation that stores every X events
/// </summary>
public class IntervalStrategy : ISnapshotStrategy
{
    private readonly int _eventsPerSnapshot;

    public IntervalStrategy(int eventsPerSnapshot)
    {
        _eventsPerSnapshot = eventsPerSnapshot;
    }

    public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => persistedEvent.Index % _eventsPerSnapshot == 0;
}