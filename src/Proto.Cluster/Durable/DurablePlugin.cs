// -----------------------------------------------------------------------
// <copyright file="DurablePlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
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

        public Task<DurableFunctionStarted> StartAsync(string kind, object arguments)
        {
            var id = Guid.NewGuid().ToString("N");
            return _cluster.RequestAsync<DurableFunctionStarted>(id, kind, arguments, CancellationToken.None);
        }

        //manages calls from a durable function to actors and activities
        internal async Task<DurableResponse> RequestAsync(DurableRequest request)
        {
            if (_cache.TryGetValue(request, out var response)) return response;

            var file = $"{request.Id}-{request.Sender.Identity}-{request.Sender.Kind}";
            var data = (request.Message as IMessage).ToByteArray();
            await File.WriteAllBytesAsync(file,data);

            var responseMessage = await _cluster.RequestAsync<object>(request.Target.Identity, request.Target.Kind, request.Message, CancellationToken.None);
            response = new DurableResponse(responseMessage);
            _cache.TryAdd(request, response);

            return response;
        }

        internal async Task PersistFunctionAsync(ClusterIdentity identity, object message)
        {
            
        }
    }
}