// -----------------------------------------------------------------------
// <copyright file="DurableContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

namespace Proto.Cluster.Durable
{
    public class DurableContext
    {
        private readonly Cluster _cluster;
        private readonly ClusterIdentity _identity;
        public object Message { get; set; }
        internal int Counter { get; set; }

        public DurableContext(Cluster cluster, ClusterIdentity identity)
        {
            _cluster = cluster;
            _identity = identity;
        }

        public Task<T> WaitForExternalEvent<T>() => null;

        public Task CreateTimer() => null;

        public async Task<T> RequestAsync<T>(string identity, string kind, object message)
        {
            //send request to local orchestrator
            //orchestrator saves request to DB
            
            //await response from orchestrator
            var target = new ClusterIdentity
            {
                Identity = identity,
                Kind = kind,
            };

            Counter++;

            var request = new DurableRequest(_identity, target, message, Counter);

            var durablePlugin = _cluster.System.Extensions.Get<DurablePlugin>();
            var response1 = await durablePlugin.DurableRequestAsync(request);
            var response = response1;
            var m = response.Message;
            return (T) m;
        }
    }
}