// -----------------------------------------------------------------------
//  <copyright file="InMemoryProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace Proto.Persistence.Tests;

public class InMemoryProvider : IProvider
{
    private readonly ConcurrentDictionary<string, Dictionary<long, object>> _events = new();

    private readonly ConcurrentDictionary<string, Dictionary<long, object>> _snapshots = new();

    public Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
    {
        if (!_snapshots.TryGetValue(actorName, out var snapshots))
        {
            return Task.FromResult<(object, long)>((null, 0));
        }

        var snapshot = snapshots.OrderBy(ss => ss.Key).LastOrDefault();

        return Task.FromResult((snapshot.Value, snapshot.Key));
    }

    public Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
    {
        var lastIndex = 0L;

        if (_events.TryGetValue(actorName, out var events))
        {
            foreach (var e in events.Where(e => e.Key >= indexStart && e.Key <= indexEnd))
            {
                lastIndex = e.Key;
                callback(e.Value);
            }
        }

        return Task.FromResult(lastIndex);
    }

    public Task<long> PersistEventAsync(string actorName, long index, object @event)
    {
        var events = _events.GetOrAdd(actorName, new Dictionary<long, object>());

        events.Add(index, @event);

        var max = events.Max(x => x.Key);

        return Task.FromResult(max);
    }

    public Task PersistSnapshotAsync(string actorName, long index, object snapshot)
    {
        var type = snapshot.GetType();
        var snapshots = _snapshots.GetOrAdd(actorName, new Dictionary<long, object>());
        var copy = JsonConvert.DeserializeObject(JsonConvert.SerializeObject(snapshot), type);

        snapshots.Add(index, copy);

        return Task.CompletedTask;
    }

    public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
    {
        if (!_events.TryGetValue(actorName, out var events))
        {
            return Task.FromResult<(object, long)>((null, 0));
        }

        var eventsToRemove = events.Where(s => s.Key <= inclusiveToIndex)
            .Select(e => e.Key)
            .ToList();

        eventsToRemove.ForEach(key => events.Remove(key));

        return Task.CompletedTask;
    }

    public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
    {
        if (!_snapshots.TryGetValue(actorName, out var snapshots))
        {
            return Task.FromResult<(object, long)>((null, 0));
        }

        var snapshotsToRemove = snapshots.Where(s => s.Key <= inclusiveToIndex)
            .Select(snapshot => snapshot.Key)
            .ToList();

        snapshotsToRemove.ForEach(key => snapshots.Remove(key));

        return Task.CompletedTask;
    }

    public Dictionary<long, object> GetSnapshots(string actorId) => _snapshots[actorId];
}