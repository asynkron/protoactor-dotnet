// -----------------------------------------------------------------------
// <copyright file="SimpleClusterContext.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;

namespace ClusterExperiment1
{
    public class SimpleClusterContext : IClusterContext
    {
        private readonly Cluster _cluster;

        public SimpleClusterContext(Cluster cluster)
        {
            _cluster = cluster;
        }
        public async Task<T> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct)
        {
            var pid = await _cluster.Config.IdentityLookup.GetAsync(clusterIdentity, CancellationToken.None);
            var res = await context.RequestAsync<T>(pid, message);
            return res;
        }
    }
}