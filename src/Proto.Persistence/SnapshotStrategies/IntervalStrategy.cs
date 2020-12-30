// -----------------------------------------------------------------------
// <copyright file="IntervalStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Persistence.SnapshotStrategies
{
    public class IntervalStrategy : ISnapshotStrategy
    {
        private readonly int _eventsPerSnapshot;

        public IntervalStrategy(int eventsPerSnapshot) => _eventsPerSnapshot = eventsPerSnapshot;

        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => persistedEvent.Index % _eventsPerSnapshot == 0;
    }
}