// -----------------------------------------------------------------------
// <copyright file="IProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence;

/// <summary>
///     Abstraction for the snapshot storage
/// </summary>
public interface ISnapshotStore
{
    /// <summary>
    ///     Gets the last available snapshot for the specified actor
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <returns>A tuple of (<see cref="Snapshot" />, last event index included in the snapshot + 1)</returns>
    Task<(object? Snapshot, long Index)> GetSnapshotAsync(string actorId);

    /// <summary>
    ///     Stores a new snapshot for the specified actor
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <param name="index">Last event included in the snapshot + 1</param>
    /// <param name="snapshot">Snapshot to store</param>
    /// <returns></returns>
    Task PersistSnapshotAsync(string actorId, long index, object snapshot);

    /// <summary>
    ///     Deletes snapshots for the specified actor
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <param name="inclusiveToIndex">
    ///     Index stored along the snapshot has to be &lt;= to the value in this parameter for the
    ///     snapshot to be deleted
    /// </param>
    /// <returns></returns>
    Task DeleteSnapshotsAsync(string actorId, long inclusiveToIndex);
}

/// <summary>
///     Abstraction for event storage. Responsible for writing and retrieving event streams.
/// </summary>
public interface IEventStore
{
    /// <summary>
    ///     Gets a stream of events for particular actor
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <param name="indexStart">Index of the first event to get (inclusive)</param>
    /// <param name="indexEnd">Index of the last event to get (inclusive)</param>
    /// <param name="callback">A callback which should be called for each read event, in the order the events are stored</param>
    /// <returns>Index of the last read event or -1 if none</returns>
    Task<long> GetEventsAsync(string actorId, long indexStart, long indexEnd, Action<object> callback);

    /// <summary>
    ///     Writes an event to event stream of particular actor
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <param name="index">
    ///     Expected index this event should be written at. This can be used for optimistic concurrency,
    ///     although most providers don't do that
    /// </param>
    /// <param name="event">Event to be written</param>
    /// <returns>Index for the next event</returns>
    Task<long> PersistEventAsync(string actorId, long index, object @event);

    /// <summary>
    ///     Deletes events from actor's event stream starting with the oldest available, ending at provided index
    /// </summary>
    /// <param name="actorId">Unique actor identifier</param>
    /// <param name="inclusiveToIndex">Inclusive index of the last event to delete</param>
    /// <returns></returns>
    Task DeleteEventsAsync(string actorId, long inclusiveToIndex);
}

/// <summary>
///     Abstraction for persistence provider
/// </summary>
public interface IProvider : IEventStore, ISnapshotStore
{
}