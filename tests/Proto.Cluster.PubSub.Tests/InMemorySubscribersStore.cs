// -----------------------------------------------------------------------
// <copyright file = "InMemoryKeyValueStore.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using Proto.Utils;

namespace Proto.Cluster.PubSub.Tests;

public class InMemorySubscribersStore : IKeyValueStore<Subscribers>
{
    private readonly ConcurrentDictionary<string, Subscribers> _store = new();

    public Task<Subscribers> GetAsync(string id, CancellationToken ct)
    {
        _store.TryGetValue(id, out var subscribers);

        return subscribers == null ? Task.FromResult(new Subscribers()) : Task.FromResult(subscribers);
    }

    public Task SetAsync(string id, Subscribers state, CancellationToken ct)
    {
        _store[id] = state;

        return Task.CompletedTask;
    }

    public Task ClearAsync(string id, CancellationToken ct)
    {
        _store.TryRemove(id, out _);

        return Task.CompletedTask;
    }
}