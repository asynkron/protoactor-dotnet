// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Seed
{
    public class SeedNodeClusterProvider : IClusterProvider
    {
        private readonly (string host, int port)[] _knownHosts;

        private readonly TimeSpan _heartbeatExpiration;
        private readonly CancellationTokenSource _cts = new();
        private PID? _pid;
        private Cluster? _cluster;

        public SeedNodeClusterProvider(params (string host, int port)[] knownHosts):this(knownHosts, null){}

        public SeedNodeClusterProvider((string host, int port)[] knownHosts, TimeSpan? heartbeatExpiration = null)
        {
            if (!knownHosts.Any())
                throw new ArgumentException("At least one known host need to be specified for seed node cluster provider");

            _knownHosts = knownHosts;
            _heartbeatExpiration = heartbeatExpiration ?? TimeSpan.FromSeconds(5);
        }

        public async Task StartMemberAsync(Cluster cluster)
        {
            _pid = cluster.System.Root.SpawnNamed(SeedNodeActor.Props(), "seed");
            _cluster = cluster;
            
            await _cluster.JoinSeed(_knownHosts);
        }

        public Task StartClientAsync(Cluster cluster) => Task.CompletedTask;

        public async Task ShutdownAsync(bool graceful)
        {
            await _cluster!.System.Root.StopAsync(_pid!);
            _cts.Cancel();
        }
    }
}