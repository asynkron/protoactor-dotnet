// -----------------------------------------------------------------------
// <copyright file="InMemoryProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Proto.Persistence;

namespace Saga
{
    public class InMemoryProvider : IProvider
    {
        public readonly ConcurrentDictionary<string, Dictionary<long, object>> Events =
            new();

        public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName) =>
            Task.FromResult(((object) default(Snapshot), 0L));

        public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            if (Events.TryGetValue(actorName, out var events))
            {
                foreach (var e in events.Where(e => e.Key >= indexStart && e.Key <= indexEnd))
                {
                    callback(e.Value);
                }
            }

            return Task.FromResult(0L);
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot) => Task.FromResult(0);

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            if (!Events.TryGetValue(actorName, out var events))
                return Task.FromResult<(object, long)>((null, 0));

            var eventsToRemove = events.Where(s => s.Key <= inclusiveToIndex)
                .Select(e => e.Key)
                .ToList();

            eventsToRemove.ForEach(key => events.Remove(key));

            return Task.FromResult(0);
        }

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex) => Task.FromResult(0L);

        Task<long> IEventStore.PersistEventAsync(string actorName, long index, object @event) => Task.FromResult(0L);

        public Task PersistEventAsync(string actorName, long index, object @event)
        {
            var events = Events.GetOrAdd(actorName, new Dictionary<long, object>());
            long nextEventIndex = 1;
            if (events.Any()) nextEventIndex = events.Last().Key + 1;
            events.Add(nextEventIndex, @event);

            return Task.FromResult(0);
        }
    }
}