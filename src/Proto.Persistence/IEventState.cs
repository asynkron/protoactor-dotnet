// -----------------------------------------------------------------------
//  <copyright file="IEventState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Persistence
{
    public interface IEventState
    {
        Task GetEventsAsync(string actorName, long indexStart, Action<object> callback);
        Task PersistEventAsync(string actorName, long index, object @event);
        Task DeleteEventsAsync(string actorName, long inclusiveToIndex);
    }
}