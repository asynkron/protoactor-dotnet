// -----------------------------------------------------------------------
// <copyright file = "SeedNodeActor.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
    private readonly ILogger _logger;
    private readonly SeedNodeClusterProviderOptions _options;
    private ImmutableList<PID> _clients = ImmutableList<PID>.Empty;
    private ClusterTopology? _latestTopology;
    private ImmutableDictionary<string, Member> _members = ImmutableDictionary<
        string,
        Member
    >.Empty;

    private SeedNodeActor(SeedNodeClusterProviderOptions options, ILogger logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task ReceiveAsync(IContext context)
    {
        return context.Message switch
        {
            //TODO: handle eventstream blocked member updates
            Started => OnStarted(context),
            Connect => OnConnect(context),
            GossipUpdate { Key: GossipKeys.Topology } update => OnTopologyUpdate(context, update),
            ClusterTopology clusterTopology => OnClusterTopology(context, clusterTopology),
            JoinRequest request => OnJoinRequest(context, request),
            JoinAsClientRequest request => OnJoinAsClientRequest(context, request),
            Terminated pid => OnClientTerminated(pid),
            _ => Task.CompletedTask
        };
    }

    private Task OnConnect(IContext context)
    {
        var connect = InnerConnect(context);
        context.ReenterAfter(
            connect,
            async task =>
            {
                var res = await task;
                if (res)
                {
                    context.Respond(new Connected());
                }
                else
                {
                    context.Respond(new FailedToConnect());
                }
            }
        );

        return Task.CompletedTask;
    }

    private async Task<bool> InnerConnect(IContext context)
    {
        _logger.LogInformation(
            "Starting via SeedNode Discovery");
        
        for (var i = 0; i < 2; i++)
        {
            var members = context.Cluster().MemberList.GetMembers();
            
            var seedNodes = await _options.Discovery.GetAll().ConfigureAwait(false);
            //remove existing nodes
            seedNodes = seedNodes.Where(m => !members.Contains(m.memberId)).ToArray();

            if (!seedNodes.Any())
            {
                continue;
            }
            
            _logger.LogInformation(
                "SeedNode Discovery found seed nodes {@Members}",
                seedNodes
            );

            if (!seedNodes.Any()) continue;
            var tasks = new List<Task<Member?>>();
            foreach (var (memberId, host, port) in seedNodes)
            {
                var task = ConnectToSeedNode(context, host, port, memberId);
                tasks.Add(task);
            }

            var res = await Task.WhenAll(tasks);

            foreach (var m in res)
            {
                if (m != null)
                {
                    SetMember(m);
                }
            }

            UpdateMemberList(context);
        }

        return true;
    }

    private async Task<Member?> ConnectToSeedNode(
        IContext context,
        string host,
        int port,
        string memberId
    )
    {
        try
        {
            var pid = PID.FromAddress(host + ":" + port, Name);
            _logger.LogInformation("Trying to join seed node {Pid}", pid);

            var res = await context.System.Root.RequestAsync<JoinResponse>(
                pid,
                new JoinRequest { Joiner = context.Cluster().MemberList.Self },
                TimeSpan.FromSeconds(5)
            );

            _logger.LogInformation("Connected to seed node {Host}:{Port}", host, port);
            return res.Member;
        }
        catch (Exception x)
        {
            x.CheckFailFast();
            await _options.Discovery.Remove(memberId);

            _logger.LogError(x, "Failed to connect to seed node {Host}:{Port}", host, port);
            context.Cluster().Remote.BlockList.Block(new[] { memberId }, "Invalid SeedNode");
            return null;
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
        _logger.LogInformation("Client {Client} left", pid.Who);

        return Task.CompletedTask;
    }

    private Task OnStarted(IContext context)
    {
        _logger.LogInformation("Started SeedNodeActor");
        SetMember(context.Cluster().MemberList.Self);
        UpdateMemberList(context);

        return Task.CompletedTask;
    }

    private Task OnJoinAsClientRequest(IContext context, JoinAsClientRequest request)
    {
        _logger.LogInformation("Got JoinRequest from Client {Client}", context.Sender!);

        if (context.Sender?.IsClientId() == true)
        {
            var clientSeed = PID.FromAddress(
                context.Sender.Address,
                $"{ActorSystem.Client}/{request.SystemId}/{SeedClientNodeActor.Name}"
            );

            context.Watch(clientSeed);
            _clients = _clients.Add(clientSeed);
            context.Respond(new JoinResponse { Member = context.Cluster().MemberList.Self });

            if (_latestTopology != null)
                context.Send(clientSeed, _latestTopology);
            else
                _logger.LogWarning("No topology available");
        }
        else if (context.Sender is not null)
        {
            var clientSeed = PID.FromAddress(
                context.Sender.Address,
                SeedClientNodeActor.Name
            );
            context.Watch(clientSeed);
            _clients = _clients.Add(clientSeed);
            context.Respond(new JoinResponse { Member = context.Cluster().MemberList.Self });

            if (_latestTopology != null)
                context.Send(clientSeed, _latestTopology);
        }

        return Task.CompletedTask;
    }

    private Task OnJoinRequest(IContext context, JoinRequest request)
    {
        _logger.LogInformation("Got JoinRequest from Member {Member}", request.Joiner);
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
            SetMember(m);

        UpdateMemberList(context);

        return Task.CompletedTask;
    }

    private Task NotifyClients(IContext context, ClusterTopology topology)
    {
        foreach (var client in _clients)
            context.Send(client, topology);

        return Task.CompletedTask;
    }

    private void SetMember(Member member)
    {
        if (_members.ContainsKey(member.Id)) return;
        _members = _members.SetItem(member.Id, member);
        _logger.LogInformation("SeedNode added member {Member}", member);
    }
    
    private void RemoveMember(Member member)
    {
        if (!_members.ContainsKey(member.Id)) return;
        _members = _members.Remove(member.Id);
        _logger.LogInformation("SeedNode removed member {Member}", member);
    }

    private void UpdateMemberList(IContext context)
    {
        var members = _members.Values.ToList();
        context.Cluster().MemberList.UpdateClusterTopology(members);
    }

    public static Props Props(SeedNodeClusterProviderOptions options, ILogger logger)
    {
        return Proto.Props.FromProducer(() => new SeedNodeActor(options, logger));
    }
}
