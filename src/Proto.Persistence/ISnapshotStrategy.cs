// -----------------------------------------------------------------------
// <copyright file="ISnapshotStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Persistence
{
    public interface ISnapshotStrategy
    {
        bool ShouldTakeSnapshot(PersistedEvent persistedEvent);
    }
}