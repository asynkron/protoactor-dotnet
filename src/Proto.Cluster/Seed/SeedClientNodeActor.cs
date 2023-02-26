// -----------------------------------------------------------------------
// <copyright file = "SeedClientNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Seed;

public class SeedClientNodeActor : IActor
{
    public const string Name = "$client_seed";
    private static readonly ILogger Logger = Log.CreateLogger<SeedClientNodeActor>();
    private readonly SeedNodeClusterProviderOptions _options;
    private ClusterTopology? _clusterTopology;
    private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;

    private SeedClientNodeActor(SeedNodeClusterProviderOptions options)
    {
        _options = options;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                         => OnStarted(),
            Connect _                       => OnConnect(context),
            ClusterTopology clusterTopology => OnClusterTopology(context, clusterTopology),
            _                               => Task.CompletedTask
        };

    public static Props Props(SeedNodeClusterProviderOptions options) =>
        Proto.Props.FromProducer(() => new SeedClientNodeActor(options));

    private async Task OnConnect(IContext context)
    {
        var (selfHost, selfPort) = context.System.GetAddress();

        foreach (var (host, port) in _options.SeedNodes)
        {
            //never connect to yourself
            if (host == selfHost && port == selfPort)
            {
                continue;
            }

            try
            {
                var pid = PID.FromAddress(host + ":" + port, SeedNodeActor.Name);

                var res = await context.System.Root.RequestAsync<JoinResponse>(pid,
                    new JoinAsClientRequest { SystemId = context.System.Id }).ConfigureAwait(false);

                _members = _members.Add(res.Member.Id, res.Member);
                context.Respond(new Connected(res.Member));

                break;
            }
            catch (Exception x)
            {
                x.CheckFailFast();
                Logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
            }
        }

        context.Respond(new FailedToConnect());
    }

    private async Task OnClusterTopology(IContext context, ClusterTopology clusterTopology)
    {
        if (_clusterTopology?.TopologyHash != clusterTopology.TopologyHash)
        {
            _clusterTopology = clusterTopology;

            foreach (var member in clusterTopology.Left)
            {
                if (_members.ContainsKey(member.Id))
                {
                    _members = _members.Remove(member.Id);
                }

                Logger.LogInformation("Removed member {member}", member);
            }

            foreach (var member in clusterTopology.Members)
            {
                if (_members.ContainsKey(member.Id))
                {
                    continue;
                }

                var pid = PID.FromAddress(member.Address, SeedNodeActor.Name);

                try
                {
                    var res = await context.RequestAsync<JoinResponse>(pid,
                        new JoinAsClientRequest { SystemId = context.System.Id },
                        new CancellationTokenSource(5000).Token).ConfigureAwait(false);

                    _members = _members.Add(res.Member.Id, res.Member);
                    Logger.LogInformation("Connected to seed node {Member}", member.Address);
                }
                catch (Exception e)
                {
                    e.CheckFailFast();
                    Logger.LogError(e, "Failed to connect to seed node {Member}", member.Address);
                }
            }

            context.System.EventStream.Publish(clusterTopology);
        }
    }

    private static Task OnStarted()
    {
        Logger.LogInformation("Started SeedClientNodeActor");

        return Task.CompletedTask;
    }
}