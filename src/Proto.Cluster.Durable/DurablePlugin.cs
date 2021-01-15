// -----------------------------------------------------------------------
// <copyright file="DurablePlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto.Extensions;
using Proto.Remote;

namespace Proto.Cluster.Durable
{
    public class DurablePlugin : IActorSystemExtension<DurablePlugin>
    {
        private readonly Dictionary<DurableRequest, DurableResponse> _cache = new();
        private readonly Cluster _cluster;

        public DurablePlugin(Cluster cluster)
        {
            _cluster = cluster;

            var files = Directory.GetFiles(".", "*.dur").OrderBy(f => f);

            foreach (var f in files)
            {
                Console.WriteLine(f);
            }
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

            

            var responseMessage = await _cluster.RequestAsync<object>(request.Target.Identity, request.Target.Kind, request.Message, CancellationToken.None);
            response = new DurableResponse(responseMessage);
            _cache.TryAdd(request, response);
            await PersistRequestAsync(request, responseMessage);

            return response;
        }

        private  async Task PersistRequestAsync(DurableRequest request, object responseMessage)
        {
            var file = $"{request.Id}-{request.Sender.Identity}-{request.Sender.Kind}.dur";
            var data = _cluster.System.Serialization().Serialize(responseMessage, _cluster.System.Serialization().DefaultSerializerId);
            await File.WriteAllBytesAsync(file, data.ToByteArray());
        }

        internal async Task PersistFunctionStartAsync(ClusterIdentity identity, object message)
        {
            
        }
    }
}