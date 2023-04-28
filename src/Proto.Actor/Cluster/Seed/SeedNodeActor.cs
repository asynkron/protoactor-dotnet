// -----------------------------------------------------------------------
// <copyright file = "SeedNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;
using Proto.Remote;

namespace Proto.Cluster.Seed;

public class SeedNodeActor : IActor
{
    public const string Name = "$server_seed";
    private static readonly ILogger Logger = Log.CreateLogger<SeedNodeActor>();
    private readonly SeedNodeClusterProviderOptions _options;
    private ImmutableList<PID> _clients = ImmutableList<PID>.Empty;
    private ClusterTopology? _lastestTopology;
    private ImmutableDictionary<string, Member> _members = ImmutableDictionary<string, Member>.Empty;

    private SeedNodeActor(SeedNodeClusterProviderOptions options)
    {
        _options = options;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            Started                                          => OnStarted(context),
            Connect                                          => OnConnect(context),
            GossipUpdate { Key: GossipKeys.Topology } update => OnTopologyUpdate(context, update),
            ClusterTopology clusterTopology                  => OnClusterTopology(context, clusterTopology),
            JoinRequest request                              => OnJoinRequest(context, request),
            JoinAsClientRequest request                      => OnJoinAsClientRequest(context, request),
            Terminated pid                                   => OnClientTerminated(pid),
            _                                                => Task.CompletedTask
        };

    private async Task OnConnect(IContext context)
    {
        var (selfHost, selfPort) = context.System.GetAddress();

        if (_options.SeedNodes.Except(new[] { (selfHost, selfPort) }).Any())
        {
            foreach (var (host, port) in _options.SeedNodes)
            {
                //never connect to yourself
                if (host == selfHost && port == selfPort)
                {
                    continue;
                }

                try
                {
                    var pid = PID.FromAddress(host + ":" + port, Name);

                    var res = await context.System.Root.RequestAsync<JoinResponse>(pid, new JoinRequest
                        {
                            Joiner = context.Cluster().MemberList.Self
                        }
                    ).ConfigureAwait(false);

                    context.Respond(new Connected(res.Member));

                    return;
                }
                catch (Exception x)
                {
                    x.CheckFailFast();
                    Logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
                }
            }

            context.Respond(new FailedToConnect());
        }
        else
        {
            context.Respond(new Connected(context.Cluster().MemberList.Self));
        }
    }

    private Task OnClusterTopology(IContext context, ClusterTopology clusterTopology)
    {
        _lastestTopology = clusterTopology;
        NotifyClients(context, clusterTopology);

        return Task.CompletedTask;
    }

    private Task OnClientTerminated(Terminated pid)
    {
        _clients = _clients.Remove(pid.Who);
        Logger.LogInformation("Client {Client} left", pid.Who);

        return Task.CompletedTask;
    }

    private Task OnStarted(IContext context)
    {
        Logger.LogInformation("Started SeedNodeActor");
        SetMember(context.Cluster().MemberList.Self);
        UpdateMemberList(context);

        return Task.CompletedTask;
    }

    private Task OnJoinAsClientRequest(IContext context, JoinAsClientRequest request)
    {
        Logger.LogInformation("Got JoinRequest from Client {Client}", context.Sender!);

        if (context.Sender?.IsClientId() == true)
        {
            var clientSeed = PID.FromAddress(context.Sender.Address,
                $"{ActorSystem.Client}/{request.SystemId}/{SeedClientNodeActor.Name}");

            context.Watch(clientSeed);
            _clients = _clients.Add(clientSeed);
            context.Respond(new JoinResponse { Member = context.Cluster().MemberList.Self });

            if (_lastestTopology != null)
            {
                context.Send(clientSeed, _lastestTopology);
            }
            else
            {
                Logger.LogWarning("No topology available.");
            }
        }
        else if (context.Sender is not null)
        {
            var clientSeed = PID.FromAddress(context.Sender.Address, SeedClientNodeActor.Name);
            context.Watch(clientSeed);
            _clients = _clients.Add(clientSeed);
            context.Respond(new JoinResponse { Member = context.Cluster().MemberList.Self });

            if (_lastestTopology != null)
            {
                context.Send(clientSeed, _lastestTopology);
            }
        }

        return Task.CompletedTask;
    }

    private Task OnJoinRequest(IContext context, JoinRequest request)
    {
        Logger.LogInformation("Got JoinRequest from Member {Member}", request.Joiner);
        SetMember(request.Joiner);
        UpdateMemberList(context);
        context.Respond(new JoinResponse { Member = context.Cluster().MemberList.Self });

        return Task.CompletedTask;
    }

    private Task OnTopologyUpdate(IContext context, GossipUpdate update)
    {
        var topology = update.Value.Unpack<ClusterTopology>();
        _lastestTopology = topology;

        foreach (var m in topology.Members)
        {
            SetMember(m);
        }

        UpdateMemberList(context);

        return Task.CompletedTask;
    }

    private Task NotifyClients(IContext context, ClusterTopology topology)
    {
        foreach (var client in _clients)
        {
            context.Send(client, topology);
        }

        return Task.CompletedTask;
    }

    private void SetMember(Member member)
    {
        if (!_members.ContainsKey(member.Id))
        {
            _members = _members.SetItem(member.Id, member);
        }
    }

    private void UpdateMemberList(IContext context)
    {
        var members = _members.Values.ToList();
        context.Cluster().MemberList.UpdateClusterTopology(members);
    }

    public static Props Props(SeedNodeClusterProviderOptions options) =>
        Proto.Props.FromProducer(() => new SeedNodeActor(options));
}