// -----------------------------------------------------------------------
//  <copyright file="IProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public interface IProvider
    {
        Task GetEventsAsync(string actorName, long indexStart, Action<object> callback);
        Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName);
        Task PersistEventAsync(string actorName, long index, object @event);
        Task PersistSnapshotAsync(string actorName, long index, object snapshot);
        Task DeleteEventsAsync(string actorName, long inclusiveToIndex);
        Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex);
    }
}