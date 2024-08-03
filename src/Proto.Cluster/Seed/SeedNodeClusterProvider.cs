// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Seed;

[PublicAPI]
public class SeedNodeClusterProvider : IClusterProvider
{
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<SeedNodeClusterProvider>();
#pragma warning restore CS0618 // Type or member is obsolete
    private readonly CancellationTokenSource _cts = new();

    private readonly SeedNodeClusterProviderOptions _options;
    private Cluster _cluster = null!;
    private PID? _pid;

    public SeedNodeClusterProvider(SeedNodeClusterProviderOptions options)
    {
        _options = options;
    }

    public async Task StartMemberAsync(Cluster cluster)
    {
        _cluster = cluster;
        var props = SeedNodeActor.Props(_options, Logger);
        _pid = cluster.System.Root.SpawnNamedSystem(props, SeedNodeActor.Name);

        cluster.System.EventStream.Subscribe<GossipUpdate>(
            x => x.Key == GossipKeys.Topology,
            x => cluster.System.Root.Send(_pid, x)
        );

        cluster.System.EventStream.Subscribe<ClusterTopology>(cluster.System.Root, _pid);
        var result = await cluster.System.Root
            .RequestAsync<object>(_pid, new Connect(), _cts.Token)
            .ConfigureAwait(false);

        switch (result)
        {
            case Connected connected:
                Logger.LogInformation("Connected to seed nodes");

                break;
            default:
                throw new Exception("Failed to join any seed node");
        }

        if (_options.Discovery != null)
        {
            var (selfHost, selfPort) = _cluster.System.GetAddress();

            await _options.Discovery.Register(_cluster.System.Id, selfHost, selfPort);
            Logger.LogInformation(
                "Registering self in SeedNode Discovery {Id} {Host}:{Port}",
                cluster.System.Id,
                selfHost,
                selfPort
            );
        }
    }

    public async Task StartClientAsync(Cluster cluster)
    {
        _cluster = cluster;
        var props = SeedClientNodeActor.Props(_options, Logger);
        _pid = cluster.System.Root.SpawnNamedSystem(props, SeedClientNodeActor.Name);
        var result = await cluster.System.Root
            .RequestAsync<object>(_pid, new Connect(), _cts.Token)
            .ConfigureAwait(false);

        switch (result)
        {
            case Connected connected:
                Logger.LogInformation("Connected to seed node");

                break;
            default:
                throw new Exception("Failed to join any seed node");
        }
    }

    public async Task ShutdownAsync(bool graceful)
    {
        if (_pid is not null && _cluster is not null)
            await _cluster.System.Root.StopAsync(_pid).ConfigureAwait(false);
        
        var (selfHost, selfPort) = _cluster.System.GetAddress();
        await _options.Discovery.Remove(_cluster!.System.Id);
        Logger.LogInformation(
            "Removing self from SeedNode Discovery {Id} {Host}:{Port}",
            _cluster.System.Id,
            selfHost,
            selfPort
        );

        _cts.Cancel();
    }

    public static IClusterProvider JoinWithDiscovery(ISeedNodeDiscovery discovery)
    {
        var options = new SeedNodeClusterProviderOptions(discovery);
        var provider = new SeedNodeClusterProvider(options);
        return provider;
    }
}
