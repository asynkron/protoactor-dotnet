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
        private readonly TimeSpan _heartbeatExpiration;
        private readonly CancellationTokenSource _cts = new();
        private PID? _pid;
        private Cluster? _cluster;

        public SeedNodeClusterProvider(TimeSpan? heartbeatExpiration) => 
            _heartbeatExpiration = heartbeatExpiration ?? TimeSpan.FromSeconds(5);

        public Task StartMemberAsync(Cluster cluster)
        {
            _pid = cluster.System.Root.SpawnNamed(SeedNodeActor.Props(), "seed");
            _cluster = cluster;

            _ = SafeTask.Run(async () => {
                    while (!_cts.IsCancellationRequested)
                    {
                        await Task.Delay(100);
                        var t = await cluster.Gossip.GetStateEntry("heartbeat");

                        var blocked = (from x in t
                                       where x.Value.Age > _heartbeatExpiration
                                       select x.Key)
                            .ToArray();

                        if (blocked.Any())
                        {
                            cluster.MemberList.UpdateBlockedMembers(blocked);
                        }
                    }
                }
            );

            return Task.CompletedTask;
        }

        public Task StartClientAsync(Cluster cluster) => Task.CompletedTask;

        public async Task ShutdownAsync(bool graceful)
        {
            await _cluster!.System.Root.StopAsync(_pid!);
           _cts.Cancel();
        }
    }
}