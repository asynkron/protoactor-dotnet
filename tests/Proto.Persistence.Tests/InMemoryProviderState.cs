// -----------------------------------------------------------------------
//  <copyright file="InMemoryProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Proto.Persistence.Tests
{
    public class InMemoryProviderState : IProviderState
    {
        private readonly ConcurrentDictionary<string, Dictionary<long, object>> _events = new ConcurrentDictionary<string, Dictionary<long, object>>();

        private readonly ConcurrentDictionary<string, Dictionary<long, object>> _snapshots = new ConcurrentDictionary<string, Dictionary<long, object>>();

        public Dictionary<long, object> GetSnapshots(string actorId)
        {
            return _snapshots[actorId];
        }
        public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            if (!_snapshots.TryGetValue(actorName, out Dictionary<long, object> snapshots))
                return Task.FromResult<(object, long)>((null, 0));

            var snapshot = snapshots.OrderBy(ss => ss.Key).LastOrDefault();
            return Task.FromResult((snapshot.Value, snapshot.Key));
        }

        public Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            if (_events.TryGetValue(actorName, out Dictionary<long, object> events))
            {
                foreach (var e in events.Where(e => e.Key > indexStart))
                {
                    callback(e.Value);
                }
            }
            return Task.FromResult(0);
        }

        public Task PersistEventAsync(string actorName, long index, object @event)
        {
            var events = _events.GetOrAdd(actorName, new Dictionary<long, object>());
            long nextEventIndex = 1;
            if (events.Any())
            {
                nextEventIndex = events.Last().Key + 1;
            }
            events.Add(nextEventIndex, @event);

            return Task.FromResult(0);
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            var type = snapshot.GetType();
            var snapshots = _snapshots.GetOrAdd(actorName, new Dictionary<long, object>());
            var copy = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(snapshot), type);

            snapshots.Add(index, copy);

            return Task.FromResult(0);
        }

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            if (!_events.TryGetValue(actorName, out Dictionary<long, object> events))
                return Task.FromResult<(object, long)>((null, 0));

            var eventsToRemove = events.Where(s => s.Key <= inclusiveToIndex)
                                             .Select(e => e.Key)
                                             .ToList();

            eventsToRemove.ForEach(key => events.Remove(key));

            return Task.FromResult(0);
        }

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            if (!_snapshots.TryGetValue(actorName, out Dictionary<long, object> snapshots))
                return Task.FromResult<(object, long)>((null, 0));

            var snapshotsToRemove = snapshots.Where(s => s.Key <= inclusiveToIndex)
                                             .Select(snapshot => snapshot.Key)
                                             .ToList();

            snapshotsToRemove.ForEach(key => snapshots.Remove(key));

            return Task.FromResult(0);
        }
    }
}