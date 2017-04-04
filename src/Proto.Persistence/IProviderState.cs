// -----------------------------------------------------------------------
//  <copyright file="IProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public interface IProviderState
    {
        Task GetEventsAsync(string actorName, long indexStart, Action<object> callback);
        Task<(object Data, long Index)> GetSnapshotAsync(string actorName);
        Task PersistEventAsync(string actorName, long index, object data);
        Task PersistSnapshotAsync(string actorName, long index, object data);
        Task DeleteEventsAsync(string actorName, long fromIndex);
        Task DeleteSnapshotsAsync(string actorName, long fromIndex);
    }
}