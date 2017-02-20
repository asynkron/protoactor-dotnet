// -----------------------------------------------------------------------
//  <copyright file="IProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Google.Protobuf;

namespace Proto.Persistence
{
    public interface IProviderState
    {
        Task GetEventsAsync(string actorName, ulong eventIndexStart, Action<object> callback);
        Task<Tuple<object, ulong>> GetSnapshotAsync(string actorName);
        int GetSnapshotInterval();
        Task PersistEventAsync(string actorName, ulong eventIndex, IMessage @event);
        Task PersistSnapshotAsync(string actorName, ulong eventIndex, IMessage snapshot);
        void Restart();
    }
}