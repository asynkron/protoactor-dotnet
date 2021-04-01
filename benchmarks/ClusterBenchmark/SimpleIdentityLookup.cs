// -----------------------------------------------------------------------
// <copyright file="InMemIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Identity;

namespace ClusterExperiment1
{
    public class SimpleIdentityLookup : IIdentityLookup
    {
        private readonly ConcurrentDictionary<ClusterIdentity, PID> _pids = new();
        private Cluster _cluster;

        public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
        {
            PID pid = _pids.GetOrAdd(clusterIdentity, ci =>
                {
                    ActivatedClusterKind kind = _cluster.GetClusterKind(clusterIdentity.Kind);
                    PID pid = _cluster.System.Root.Spawn(kind.Props);
                    return pid;
                }
            );

            return Task.FromResult(pid);
        }

        public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct) =>
            //_pids.TryRemove(clusterIdentity, out _);
            Task.CompletedTask;

        public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        {
            _cluster = cluster;
            return Task.CompletedTask;
        }

        public Task ShutdownAsync() => Task.CompletedTask;
    }
}
