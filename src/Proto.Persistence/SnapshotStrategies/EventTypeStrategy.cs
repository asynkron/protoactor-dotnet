// -----------------------------------------------------------------------
// <copyright file="EventTypeStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Persistence.SnapshotStrategies
{
    public class EventTypeStrategy : ISnapshotStrategy
    {
        private readonly Type _eventType;

        public EventTypeStrategy(Type eventType) => _eventType = eventType;

        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent) => persistedEvent.Data.GetType() == _eventType;
    }
}