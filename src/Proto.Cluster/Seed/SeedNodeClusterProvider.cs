// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Seed
{
    public class SeedNodeClusterProvider : IClusterProvider
    {
        private readonly TimeSpan _heartbeatExpiration;
        private readonly CancellationTokenSource _cts = new();
        private PID? _pid;
        private Cluster? _cluster;
        private static readonly ILogger Logger = Log.CreateLogger<SeedNodeClusterProvider>(); 

        public SeedNodeClusterProvider(TimeSpan? heartbeatExpiration=null) => 
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
                            Logger.LogInformation("Blocking members due to expired heartbeat {Members}", blocked);
                            cluster.MemberList.UpdateBlockedMembers(blocked);
                        }

                        var t2 = await cluster.Gossip.GetStateEntry("cluster:left");
                        
                        //don't ban ourselves. our gossip state will never reach other members then...
                        var gracefullyLeft = t2.Keys.Where(k => k != cluster.System.Id) .ToArray();

                        if (gracefullyLeft.Any())
                        {
                            Logger.LogInformation("Blocking members due to gracefully leaving {Members}", gracefullyLeft);
                            cluster.MemberList.UpdateBlockedMembers(gracefullyLeft);
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