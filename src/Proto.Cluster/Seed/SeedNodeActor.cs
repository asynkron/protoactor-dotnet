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
    private ClusterTopology? _latestTopology;
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

        (string, string, int)[] seedNodes;

        if (_options.Discovery != null)
        {
            seedNodes = await _options.Discovery.GetAll().ConfigureAwait(false);
            Logger.LogInformation("Starting via SeedNode Discovery, found seed nodes {@Members}", seedNodes);
        }
        else
        {
            Logger.LogInformation("Starting via SeedNode, found seed nodes {@Members}", _options.SeedNodes);
            //hack: manual seed nodes does not care about member id, but we need consistent data below
            seedNodes = _options.SeedNodes.Except(new[] { (selfHost, selfPort) }).Select(t => ("",t.Item1,t.Item2)).ToArray();
        }

        if (seedNodes.Any())
        {
            foreach (var (memberid, host, port) in seedNodes)
            {
                //never connect to yourself
                if (host == selfHost && port == selfPort)
                {
                    Logger.LogInformation("Skipping self seed node {Host}:{Port}", host, port);
                    continue;
                }

                try
                {
                    Logger.LogInformation("Creating pid...");
                    var pid = PID.FromAddress(host + ":" + port, Name);
                    Logger.LogInformation("Trying to join seed node {Pid}", pid);

                    var res = await context.System.Root.RequestAsync<JoinResponse>(pid, new JoinRequest
                        {
                            Joiner = context.Cluster().MemberList.Self
                        }
                    , TimeSpan.FromSeconds(10));

                    context.Respond(new Connected(res.Member));

                    return;
                }
                catch (Exception x)
                {
                    x.CheckFailFast();
                    if (_options.Discovery is not null)
                    {
                        await _options.Discovery.Remove(memberid);
                    }
                    Logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
                }
            }

            if (_options.Discovery is not null)
            {
                //stale members found, couldn't connect, mark ourselves as seednode
                context.Respond(new Connected(context.Cluster().MemberList.Self));
            }
            else
            {
                context.Respond(new FailedToConnect());
            }
        }
        else
        {
            context.Respond(new Connected(context.Cluster().MemberList.Self));
        }
    }

    private async Task OnClusterTopology(IContext context, ClusterTopology clusterTopology)
    {
        _latestTopology = clusterTopology;
        await NotifyClients(context, clusterTopology);
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

            if (_latestTopology != null)
            {
                context.Send(clientSeed, _latestTopology);
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

            if (_latestTopology != null)
            {
                context.Send(clientSeed, _latestTopology);
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
        _latestTopology = topology;

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