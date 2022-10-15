// -----------------------------------------------------------------------
// <copyright file="MemberList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Proto.Mailbox;
using Proto.Remote;

namespace Proto.Cluster;

/// <summary>
///     Responsible for figuring out what members are currently active in the cluster.
///     It will receive a list of Members from the IClusterProvider and from that, we calculate a delta, which members
///     joined, or left.
///     This also subscribes to gossip messages from the cluster, and updates the <see cref="BlockList" /> accordingly.
///     If the member learns that it is blocked from gossip, it will initiate shutdown.
/// </summary>
[PublicAPI]
public record MemberList
{
    private static readonly ILogger Logger = Log.CreateLogger<MemberList>();
    private readonly Cluster _cluster;
    private readonly EventStream _eventStream;
    private readonly object _lock = new();

    private readonly IRootContext _root;
    private readonly ActorSystem _system;

    // private Member? _leader;

    //TODO: the members here are only from the cluster provider
    //The partition lookup broadcasts and use broadcasted information
    //meaning the partition infra might be ahead of this list.
    //come up with a good solution to keep all this in sync
    private ImmutableMemberSet _activeMembers = ImmutableMemberSet.Empty;
    private CancellationTokenSource? _currentTopologyTokenSource;
    private ImmutableDictionary<string, int> _indexByAddress = ImmutableDictionary<string, int>.Empty;

    private ImmutableDictionary<int, Member> _membersByIndex = ImmutableDictionary<int, Member>.Empty;

    private ImmutableDictionary<string, IMemberStrategy> _memberStrategyByKind =
        ImmutableDictionary<string, IMemberStrategy>.Empty;

    private ImmutableDictionary<string, MetaMember> _metaMembers = ImmutableDictionary<string, MetaMember>.Empty;

    private int _nextMemberIndex;

    private TaskCompletionSource<bool> _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private IConsensusHandle<ulong>? _topologyConsensus;

    public MemberList(Cluster cluster)
    {
        _cluster = cluster;
        _system = _cluster.System;
        _root = _system.Root;
        var (host, port) = _cluster.System.GetAddress();

        Self = new Member
        {
            Id = _cluster.System.Id,
            Host = host,
            Port = port,
            Kinds = { _cluster.GetClusterKinds() }
        };

        _eventStream = _system.EventStream;

        //subscribe non synchronous to avoid recursive updates
        _eventStream.Subscribe<GossipUpdate>(u =>
            {
                if (u.Key != GossipKeys.Topology)
                {
                    return;
                }

                //get blocked members from all other member states, and merge that with our own blocked set
                var topology = u.Value.Unpack<ClusterTopology>();
                var blocked = topology.Blocked.ToArray();
                _cluster.Remote.BlockList.Block(blocked);
            }
        );

        _eventStream.Subscribe<MemberBlocked>(b =>
            {
                if (b.MemberId == _system.Id)
                {
                    SelfBlocked();
                }

                UpdateClusterTopology(_activeMembers.Members);
            }, Dispatchers.DefaultDispatcher
        );
    }

    /// <summary>
    ///     Gets the current member
    /// </summary>
    public Member Self { get; }

    public Task Started => _startedTcs.Task;

    public string MemberId => _system.Id;

    public bool Stopping { get; internal set; }

    /// <summary>
    ///     Gets a list of member ids (same as <see cref="ActorSystem.Id" />) that are currently active in the cluster.
    /// </summary>
    /// <returns></returns>
    public ImmutableHashSet<string> GetMembers() => _activeMembers.Members.Select(m => m.Id).ToImmutableHashSet();

    internal void InitializeTopologyConsensus() =>
        _topologyConsensus =
            _cluster.Gossip.RegisterConsensusCheck<ClusterTopology, ulong>(GossipKeys.Topology,
                topology => topology.TopologyHash);

    internal Task<(bool consensus, ulong topologyHash)> TopologyConsensus(CancellationToken ct) =>
        _topologyConsensus?.TryGetConsensus(ct) ??
        Task.FromResult<(bool consensus, ulong topologyHash)>(default);

    internal Member? GetActivator(string kind, string requestSourceAddress)
    {
        //immutable, don't lock
        if (_memberStrategyByKind.TryGetValue(kind, out var memberStrategy))
        {
            return memberStrategy.GetActivator(requestSourceAddress);
        }

        Logger.LogInformation("MemberList did not find any activator for kind '{Kind}'", kind);

        return null;
    }

    /// <summary>
    ///     Used by clustering providers to update the member list.
    /// </summary>
    /// <param name="members"></param>
    public void UpdateClusterTopology(IReadOnlyCollection<Member> members)
    {
        var blockList = _system.Remote().BlockList;

        lock (_lock)
        {
            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[MemberList] Updating Cluster Topology");
            }

            if (blockList.IsBlocked(_system.Id))
            {
                SelfBlocked();

                return;
            }

            //TLDR:
            //this method filters out any member status in the blocked list
            //then makes a delta between new and old members
            //notifying the cluster accordingly which members left or joined

            var activeMembers = new ImmutableMemberSet(members.ToArray()).Except(blockList.BlockedMembers);

            if (activeMembers.Equals(_activeMembers))
            {
                return;
            }

            // Cancel any work based on the previous topology
            _currentTopologyTokenSource?.Cancel();
            _currentTopologyTokenSource = new CancellationTokenSource();

            var left = _activeMembers.Except(activeMembers);
            var joined = activeMembers.Except(_activeMembers);
            blockList.Block(left.Members.Select(m => m.Id));
            _activeMembers = activeMembers;

            //notify that these members left
            foreach (var memberThatLeft in left.Members)
            {
                MemberLeave(memberThatLeft);
                TerminateMember(memberThatLeft);
            }

            //notify that these members joined
            foreach (var memberThatJoined in joined.Members)
            {
                MemberJoin(memberThatJoined);
            }

            var topology = new ClusterTopology
            {
                TopologyHash = activeMembers.TopologyHash,
                Members = { activeMembers.Members },
                Left = { left.Members },
                Joined = { joined.Members },
                Blocked = { blockList.BlockedMembers },
                TopologyValidityToken = _currentTopologyTokenSource.Token
            };

            if (Logger.IsEnabled(LogLevel.Debug))
            {
                Logger.LogDebug("[MemberList] Published ClusterTopology event {ClusterTopology}", topology);
            }

            if (topology.Joined.Any())
            {
                Logger.LogInformation("[MemberList] Cluster members joined {MembersJoined}", topology.Joined);
            }

            if (topology.Left.Any())
            {
                Logger.LogInformation("[MemberList] Cluster members left {MembersLeft}", topology.Left);
            }

            BroadcastTopologyChanges(topology);

            if (!_startedTcs.Task.IsCompleted)
            {
                if (activeMembers.Contains(_system.Id))
                {
                    _startedTcs.TrySetResult(true);
                }
            }
        }

        void MemberLeave(Member memberThatLeft)
        {
            //update MemberStrategy
            foreach (var k in memberThatLeft.Kinds)
            {
                if (!_memberStrategyByKind.TryGetValue(k, out var ms))
                {
                    continue;
                }

                ms.RemoveMember(memberThatLeft);

                if (ms.GetAllMembers().Count == 0)
                {
                    _memberStrategyByKind = _memberStrategyByKind.Remove(k);
                }
            }

            if (_metaMembers.TryGetValue(memberThatLeft.Id, out var meta))
            {
                _membersByIndex = _membersByIndex.Remove(meta.Index);

                if (_indexByAddress.TryGetValue(memberThatLeft.Address, out _))
                {
                    _indexByAddress = _indexByAddress.Remove(memberThatLeft.Address);
                }
            }
            //Log?
        }

        void MemberJoin(Member newMember)
        {
            var index = _nextMemberIndex++;
            _metaMembers = _metaMembers.Add(newMember.Id, new MetaMember(newMember, index));
            _membersByIndex = _membersByIndex.Add(index, newMember);
            _indexByAddress = _indexByAddress.Add(newMember.Address, index);

            foreach (var kind in newMember.Kinds)
            {
                if (!_memberStrategyByKind.ContainsKey(kind))
                {
                    _memberStrategyByKind = _memberStrategyByKind.SetItem(kind, GetMemberStrategyByKind(kind));
                }

                _memberStrategyByKind[kind].AddMember(newMember);
            }
        }
    }

    private void SelfBlocked()
    {
        // If already shutting down, nothing to do.
        if (Stopping || _system.Shutdown.IsCancellationRequested)
        {
            return;
        }

        Logger.LogCritical("I have been blocked, exiting {Id}", MemberId);
        _ = _cluster.ShutdownAsync(reason: "Blocked by MemberList");
    }

    internal MetaMember? GetMetaMember(string memberId)
    {
        _metaMembers.TryGetValue(memberId, out var meta);

        return meta;
    }

    private void BroadcastTopologyChanges(ClusterTopology topology)
    {
        _system.Logger()?.LogDebug("MemberList sending state");
        _eventStream.Publish(topology);

        //Console.WriteLine($"{_system.Id} Broadcasting {topology.TopologyHash} - {topology.Members.Count}");
    }

    private void TerminateMember(Member memberThatLeft)
    {
        var endpointTerminated = new EndpointTerminatedEvent(true, memberThatLeft.Address, memberThatLeft.Id);

        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.LogInformation("[MemberList] Published event {@EndpointTerminated}", endpointTerminated);
        }

        _cluster.System.EventStream.Publish(endpointTerminated);
    }

    private IMemberStrategy GetMemberStrategyByKind(string kind)
    {
        //Try get the cluster kind
        var clusterKind = _cluster.TryGetClusterKind(kind);

        //if it exists, and if it has a strategy
        if (clusterKind?.Strategy != null)
        {
            //use that strategy
            return clusterKind.Strategy;
        }

        //otherwise, use whatever member strategy the default builder says
        return _cluster.Config.MemberStrategyBuilder(_cluster, kind) ?? new SimpleMemberStrategy();
    }

    /// <summary>
    ///     Broadcast a message to all members' <see cref="EventStream" />
    /// </summary>
    /// <param name="message">Message to broadcast</param>
    /// <param name="includeSelf">If true, message will also be sent to this member's <see cref="EventStream" /></param>
    public void BroadcastEvent(object message, bool includeSelf = true)
    {
        foreach (var (id, member) in _activeMembers.Lookup)
        {
            if (!includeSelf && id == _cluster.System.Id)
            {
                continue;
            }

            var pid = PID.FromAddress(member.Address, "$eventstream");

            try
            {
                _system.Root.Send(pid, message);
            }
            catch (Exception x)
            {
                x.CheckFailFast();
                Logger.LogError(x, "[MemberList] Failed to broadcast {Message} to {Pid}", message, pid);
            }
        }
    }

    /// <summary>
    ///     Returns true if the member is in the active member list, false otherwise.
    /// </summary>
    /// <param name="memberId">Member id</param>
    /// <returns></returns>
    public bool ContainsMemberId(string memberId) => _activeMembers.Contains(memberId);

    /// <summary>
    ///     Tries to get the member by id and returns true if it was found, false otherwise.
    /// </summary>
    /// <param name="memberId">Member id</param>
    /// <param name="value">Used to return the member</param>
    /// <returns></returns>
    public bool TryGetMember(string memberId, out Member? value) =>
        _activeMembers.Lookup.TryGetValue(memberId, out value);

    internal bool TryGetMemberIndexByAddress(string address, out int value) =>
        _indexByAddress.TryGetValue(address, out value);

    internal bool TryGetMemberByIndex(int memberIndex, out Member? value) =>
        _membersByIndex.TryGetValue(memberIndex, out value);

    /// <summary>
    ///     Gets a list of active <see cref="Member" />
    /// </summary>
    /// <returns></returns>
    public Member[] GetAllMembers() => _activeMembers.Members.ToArray();

    /// <summary>
    ///     Gets a list of active <see cref="Member" /> apart from the current one
    /// </summary>
    /// <returns></returns>
    public Member[] GetOtherMembers() => _activeMembers.Members.Where(m => m.Id != _system.Id).ToArray();

    /// <summary>
    ///     Gets a list of <see cref="Member" /> that support spawning virtual actors of given cluster kind
    /// </summary>
    /// <param name="kind"></param>
    /// <returns></returns>
    public Member[] GetMembersByKind(string kind) =>
        _activeMembers.Members.Where(m => m.Kinds.Contains(kind)).ToArray();
}
