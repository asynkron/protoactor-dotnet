// -----------------------------------------------------------------------
// <copyright file="ISnapshotStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

namespace Proto.Persistence;

/// <summary>
///     A strategy that decides at what points in time to take snapshots
/// </summary>
public interface ISnapshotStrategy
{
    /// <summary>
    ///     Returns true if for given <see cref="PersistedEvent" /> a snapshot should be stored
    /// </summary>
    /// <param name="persistedEvent">Event being persisted along with its index</param>
    /// <returns>True if snapshot should be stored</returns>
    bool ShouldTakeSnapshot(PersistedEvent persistedEvent);
}