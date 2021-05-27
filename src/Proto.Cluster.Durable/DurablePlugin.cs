// -----------------------------------------------------------------------
// <copyright file="DurablePlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Cluster.Durable
{
    public class DurablePlugin : IActorSystemExtension<DurablePlugin>
    {
        private readonly Dictionary<DurableRequest, DurableResponse> _cache = new();
        private readonly Cluster _cluster;

        public DurablePlugin(Cluster cluster)
        {
            _cluster = cluster;
        }

        public async Task<DurableResponse> DurableRequestAsync(DurableRequest request)
        {
            if (_cache.TryGetValue(request, out var response)) return response;

            var responseMessage = await _cluster.RequestAsync<object>(request.Target.Identity, request.Target.Kind, request.Message, CancellationToken.None);
            response = new DurableResponse(responseMessage);
            _cache.TryAdd(request, response);

            return response;
        }
    }
}