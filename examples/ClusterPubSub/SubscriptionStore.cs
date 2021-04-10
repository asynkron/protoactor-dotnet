// -----------------------------------------------------------------------
// <copyright file="SubscriptionStore.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster;
using Proto.Utils.Proto.Utils;

namespace ClusterPubSub
{
    public class SubscriptionStore : IKeyValueStore<Subscribers>
    {
        public Task<Subscribers> GetAsync(string id, CancellationToken ct) => Task.FromResult(new Subscribers());

        public Task SetAsync(string id, Subscribers state, CancellationToken ct) => Task.CompletedTask;

        public Task ClearAsync(string id, CancellationToken ct) => Task.CompletedTask;
    }
}