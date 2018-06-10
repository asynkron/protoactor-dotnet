// -----------------------------------------------------------------------
//  <copyright file="IProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public interface ISnapshotStore
    {
        Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName);
        Task PersistSnapshotAsync(string actorName, long index, object snapshot);
        Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex);
    }

    public interface IEventStore
    {
        Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback);
        Task<long> PersistEventAsync(string actorName, long index, object @event);
        Task DeleteEventsAsync(string actorName, long inclusiveToIndex);
    }

    public interface IProvider : IEventStore, ISnapshotStore
    {
    }
}