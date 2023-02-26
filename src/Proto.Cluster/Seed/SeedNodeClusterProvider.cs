// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Seed;

public class SeedNodeClusterProvider : IClusterProvider
{
    public static IClusterProvider JoinSeedNode(string address, int port)
    {
        return new SeedNodeClusterProvider(new SeedNodeClusterProviderOptions((address, port)));
    }
    
    public static IClusterProvider StartSeedNode()
    {
        return new SeedNodeClusterProvider();
    }
    
    private static readonly ILogger Logger = Log.CreateLogger<SeedNodeClusterProvider>();
    private readonly CancellationTokenSource _cts = new();

    private readonly SeedNodeClusterProviderOptions _options;
    private Cluster? _cluster;
    private PID? _pid;

    public SeedNodeClusterProvider(SeedNodeClusterProviderOptions? options = null)
    {
        _options = options ?? new SeedNodeClusterProviderOptions();
    }

    public async Task StartMemberAsync(Cluster cluster)
    {
        _cluster = cluster;
        _pid = cluster.System.Root.SpawnNamedSystem(SeedNodeActor.Props(_options), SeedNodeActor.Name);

        cluster.System.EventStream.Subscribe<GossipUpdate>(x => x.Key == GossipKeys.Topology,
            x => cluster.System.Root.Send(_pid, x));

        cluster.System.EventStream.Subscribe<ClusterTopology>(cluster.System.Root, _pid);
        var result = await cluster.System.Root.RequestAsync<object>(_pid, new Connect(), _cts.Token).ConfigureAwait(false);

        switch (result)
        {
            case Connected connected:
                Logger.LogInformation("Connected to seed node {MemberAddress}", connected.Member.Address);

                break;
            default:
                throw new Exception("Failed to join any seed node");
        }
    }

    public async Task StartClientAsync(Cluster cluster)
    {
        _cluster = cluster;
        _pid = cluster.System.Root.SpawnNamedSystem(SeedClientNodeActor.Props(_options), SeedClientNodeActor.Name);
        var result = await cluster.System.Root.RequestAsync<object>(_pid, new Connect(), _cts.Token).ConfigureAwait(false);

        switch (result)
        {
            case Connected connected:
                Logger.LogInformation("Connected to seed node {MemberAddress}", connected.Member.Address);

                break;
            default:
                throw new Exception("Failed to join any seed node");
        }
    }

    public async Task ShutdownAsync(bool graceful)
    {
        if (_pid is not null && _cluster is not null)
        {
            await _cluster.System.Root.StopAsync(_pid).ConfigureAwait(false);
        }

        _cts.Cancel();
    }
}