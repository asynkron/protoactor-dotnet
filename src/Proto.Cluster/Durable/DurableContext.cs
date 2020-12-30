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

        public DurableContext(Cluster cluster, ClusterIdentity identity)
        {
            _cluster = cluster;
            _identity = identity;
        }

        public Task<T> WaitForExternalEvent<T>()
        {
            return null;
        }

        public Task CreateTimer()
        {
            return null;
        }
        
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

            var request = new DurableRequest(_identity, target, message);

            var response = await _cluster.DurableRequestAsync(request);
            var m = response.Message;
            return (T) m;
        }
    }
}