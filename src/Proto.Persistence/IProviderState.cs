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
        Task GetEventsAsync(string actorName, ulong eventIndexStart, Action<object> callback);
        Task<Tuple<object, ulong>> GetSnapshotAsync(string actorName);
        int GetSnapshotInterval();
        Task PersistEventAsync(string actorName, ulong eventIndex, object @event);
        Task PersistSnapshotAsync(string actorName, ulong eventIndex, object snapshot);
        void Restart();
    }
}