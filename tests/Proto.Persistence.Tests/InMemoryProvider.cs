// -----------------------------------------------------------------------
//  <copyright file="InMemoryProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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
    public class InMemoryProvider : IProvider
    {
        private readonly ConcurrentDictionary<string, Dictionary<long, object>> _events = new();

        private readonly ConcurrentDictionary<string, Dictionary<long, object>> _snapshots = new();

        public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            if (!_snapshots.TryGetValue(actorName, out Dictionary<long, object> snapshots))
            {
                return Task.FromResult<(object, long)>((null, 0));
            }

            KeyValuePair<long, object> snapshot = snapshots.OrderBy(ss => ss.Key).LastOrDefault();
            return Task.FromResult((snapshot.Value, snapshot.Key));
        }

        public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            long lastIndex = 0l;
            if (_events.TryGetValue(actorName, out Dictionary<long, object> events))
            {
                foreach (KeyValuePair<long, object> e in events.Where(e => e.Key >= indexStart && e.Key <= indexEnd))
                {
                    lastIndex = e.Key;
                    callback(e.Value);
                }
            }

            return Task.FromResult(lastIndex);
        }

        public Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            Dictionary<long, object> events = _events.GetOrAdd(actorName, new Dictionary<long, object>());

            events.Add(index, @event);

            long max = events.Max(x => x.Key);

            return Task.FromResult(max);
        }

        public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            Type type = snapshot.GetType();
            Dictionary<long, object> snapshots = _snapshots.GetOrAdd(actorName, new Dictionary<long, object>());
            object? copy = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(snapshot), type);

            snapshots.Add(index, copy);

            return Task.CompletedTask;
        }

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            if (!_events.TryGetValue(actorName, out Dictionary<long, object> events))
            {
                return Task.FromResult<(object, long)>((null, 0));
            }

            List<long> eventsToRemove = events.Where(s => s.Key <= inclusiveToIndex)
                .Select(e => e.Key)
                .ToList();

            eventsToRemove.ForEach(key => events.Remove(key));

            return Task.CompletedTask;
        }

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            if (!_snapshots.TryGetValue(actorName, out Dictionary<long, object> snapshots))
            {
                return Task.FromResult<(object, long)>((null, 0));
            }

            List<long> snapshotsToRemove = snapshots.Where(s => s.Key <= inclusiveToIndex)
                .Select(snapshot => snapshot.Key)
                .ToList();

            snapshotsToRemove.ForEach(key => snapshots.Remove(key));

            return Task.CompletedTask;
        }

        public Dictionary<long, object> GetSnapshots(string actorId) => _snapshots[actorId];
    }
}
