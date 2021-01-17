// -----------------------------------------------------------------------
// <copyright file="DurablePlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster.Durable.FileSystem;
using Proto.Extensions;

namespace Proto.Cluster.Durable
{
    public class DurablePlugin : ActorSystemExtension<DurablePlugin>
    {
        private readonly Dictionary<DurableRequest, DurableResponse> _cache = new();
        private readonly Cluster _cluster;
        private readonly IDurablePersistence _durablePersistence;

        public DurablePlugin(Cluster cluster, IDurablePersistence durablePersistence)
        {
            _cluster = cluster;
            _durablePersistence = durablePersistence;
            _ = _durablePersistence.StartAsync(_cluster);
        }

        public Task<DurableFunctionStarted> StartAsync(string kind, object arguments)
        {
            var id = Guid.NewGuid().ToString("N");
            return _cluster.RequestAsync<DurableFunctionStarted>(id, kind, arguments, CancellationToken.None);
        }

        //manages calls from a durable function to actors and activities
        internal async Task<DurableResponse> RequestAsync(DurableRequest request)
        {
            if (_cache.TryGetValue(request, out var response)) return response;

            var responseMessage =
                await _cluster.RequestAsync<object>(request.Target.Identity, request.Target.Kind, request.Message, CancellationToken.None);
            response = new DurableResponse(responseMessage);

            if (_cache.TryAdd(request, response)) await _durablePersistence.PersistRequestAsync(request, responseMessage);

            return response;
        }

        internal Task PersistFunctionStartAsync(ClusterIdentity identity, object message)
            => _durablePersistence.PersistFunctionStartAsync(identity, message);
    }
}