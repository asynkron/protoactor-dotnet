// -----------------------------------------------------------------------
// <copyright file = "SeedClientNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
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
    private readonly ILogger _logger;
    private readonly SeedNodeClusterProviderOptions _options;
    private ClusterTopology? _clusterTopology;
    private ImmutableDictionary<string, Member> _members = ImmutableDictionary<
        string,
        Member
    >.Empty;

    private SeedClientNodeActor(
        SeedNodeClusterProviderOptions options,
        ILogger logger
    )
    {
        _options = options;
        _logger = logger;
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            Started => OnStarted(),
            Connect _ => OnConnect(context),
            ClusterTopology clusterTopology => OnClusterTopology(context, clusterTopology),
            _ => Task.CompletedTask
        };
    }

    public static Props Props(SeedNodeClusterProviderOptions options, ILogger logger)
    {
        return Proto.Props.FromProducer(() => new SeedClientNodeActor(options, logger));
    }

    private async Task OnConnect(IContext context)
    {
        var (selfHost, selfPort) = context.System.GetAddress();

        bool connected = false;
        // foreach (var (host, port) in _options.SeedNodes)
        // {
        //     //never connect to yourself
        //     if (host == selfHost && port == selfPort)
        //         continue;
        //
        //     try
        //     {
        //         var pid = PID.FromAddress(host + ":" + port, SeedNodeActor.Name);
        //
        //         var res = await context.System.Root
        //             .RequestAsync<JoinResponse>(
        //                 pid,
        //                 new JoinAsClientRequest { SystemId = context.System.Id }
        //             )
        //             .ConfigureAwait(false);
        //
        //         _members = _members.Add(res.Member.Id, res.Member);
        //
        //         connected = true;
        //         _logger.LogInformation("Connected to seed node {Host}:{Port}", host, port);
        //
        //         break;
        //     }
        //     catch (Exception x)
        //     {
        //         x.CheckFailFast();
        //         _logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
        //     }
        // }

        if (connected)
        {
            context.Respond(new Connected());
        }
        else
        {
            context.Respond(new FailedToConnect());
        }
    }

    private async Task OnClusterTopology(IContext context, ClusterTopology clusterTopology)
    {
        if (_clusterTopology?.TopologyHash != clusterTopology.TopologyHash)
        {
            _clusterTopology = clusterTopology;

            foreach (var member in clusterTopology.Left)
            {
                if (_members.ContainsKey(member.Id))
                    _members = _members.Remove(member.Id);

                _logger.LogInformation("Removed member {Member}", member);
            }

            foreach (var member in clusterTopology.Members)
            {
                if (_members.ContainsKey(member.Id))
                    continue;

                var pid = PID.FromAddress(member.Address, SeedNodeActor.Name);

                try
                {
                    var res = await context
                        .RequestAsync<JoinResponse>(
                            pid,
                            new JoinAsClientRequest { SystemId = context.System.Id },
                            new CancellationTokenSource(5000).Token
                        )
                        .ConfigureAwait(false);

                    _members = _members.Add(res.Member.Id, res.Member);
                    _logger.LogInformation("Connected to seed node {Member}", member.Address);
                }
                catch (Exception e)
                {
                    e.CheckFailFast();
                    _logger.LogError(e, "Failed to connect to seed node {Member}", member.Address);
                }
            }

            context.System.EventStream.Publish(clusterTopology);
        }
    }

    private Task OnStarted()
    {
        _logger.LogInformation("Started SeedClientNodeActor");

        return Task.CompletedTask;
    }
}
