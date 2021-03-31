// -----------------------------------------------------------------------
// <copyright file="InMemIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Identity;

namespace ClusterExperiment1
{
    public class InMemIdentityLookup : IIdentityLookup
    {
        private readonly ConcurrentDictionary<ClusterIdentity, PID> _pids = new ConcurrentDictionary<ClusterIdentity, PID>();
        private Cluster _cluster;
        public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            var pid = _pids.GetOrAdd(clusterIdentity, ci => {
                    var kind = _cluster.GetClusterKind(clusterIdentity.Kind);
                    var pid = _cluster.System.Root.Spawn(kind.Props);
                    return pid;
                }
            );

            return Task.FromResult(pid);
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        {
            //_pids.TryRemove(clusterIdentity, out _);
            return Task.CompletedTask;
        }

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync() => Task.CompletedTask;
    }
}