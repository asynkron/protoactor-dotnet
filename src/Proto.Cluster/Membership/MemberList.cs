// -----------------------------------------------------------------------
// <copyright file="MemberList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
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
public record MemberList : IMemberList
{
#pragma warning disable CS0618 // Type or member is obsolete
    private static readonly ILogger Logger = Log.CreateLogger<MemberList>();
#pragma warning restore CS0618 // Type or member is obsolete
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

    private ImmutableDictionary<string, MetaMember> _metaMembers = ImmutableDictionary<string, MetaMember>.Empty;

    private int _nextMemberIndex;

    private TaskCompletionSource<bool> _startedTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ConsensusManager _consensusManager;
    private readonly MemberStrategyManager _memberStrategyManager;
    private readonly bool _isClient;

    public bool IsClient => _isClient;

    public MemberList(Cluster cluster, bool isClient = false)
    {
        _cluster = cluster;
        _system = _cluster.System;
        _root = _system.Root;
        _isClient = isClient;
        var (host, port) = _cluster.System.GetAddress();

        Self = new Member
        {
            Id = _cluster.System.Id,
            Host = host,
            Port = port,
            Kinds = { _cluster.GetClusterKinds() }
        };

        _consensusManager = new ConsensusManager(cluster);
        _memberStrategyManager = new MemberStrategyManager(cluster);
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
                _cluster.Remote.BlockList.Block(blocked, "Blocked via gossip");
            }
        );

        _eventStream.Subscribe<MemberBlocked>(b =>
            {
                if (b.MemberId == _system.Id)
                {
                    SelfBlocked();
                }

                //only log if the member is known to us
                if (TryGetMember(b.MemberId, out _))
                {
                    Logger.BlockingMemberDueToReason(b.MemberId, b.Reason);
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

    internal void InitializeTopologyConsensus() => _consensusManager.InitializeTopologyConsensus();

    internal Task<(bool consensus, ulong topologyHash)> TopologyConsensus(CancellationToken ct) =>
        _consensusManager.TopologyConsensus(ct);

    internal Member? GetActivator(string kind, string requestSourceAddress) =>
        _memberStrategyManager.GetActivator(kind, requestSourceAddress);

    /// <summary>
    ///     Used by clustering providers to update the member list.
    /// </summary>
    /// <param name="members"></param>
    public void UpdateClusterTopology(IReadOnlyCollection<Member> members)
    {
        var blockList = _system.Remote().BlockList;

        lock (_lock)
        {
            Logger.LogDebug("[MemberList] Updating Cluster Topology");

            if (blockList.IsBlocked(_system.Id))
            {
                SelfBlocked();
                return;
            }

            var changes = ClusterTopologyBuilder.Compute(_activeMembers, members, blockList.BlockedMembers);

            if (changes.ActiveMembers.Equals(_activeMembers))
            {
                return;
            }

            _currentTopologyTokenSource?.Cancel();
            _currentTopologyTokenSource = new CancellationTokenSource();

            blockList.Block(changes.Left.Members.Select(m => m.Id), "Member left cluster");
            _activeMembers = changes.ActiveMembers;

            foreach (var member in changes.Left.Members)
            {
                HandleMemberLeave(member);
                TerminateMember(member);
            }

            foreach (var member in changes.Joined.Members)
            {
                HandleMemberJoin(member);
            }

            var topology = ClusterTopologyBuilder.BuildTopology(
                changes,
                blockList.BlockedMembers,
                _currentTopologyTokenSource.Token
            );

            LogTopologyChanges(topology);
            BroadcastTopologyChanges(topology);
            TrySetStarted(changes.ActiveMembers);
        }
    }

    private void HandleMemberLeave(Member memberThatLeft)
    {
        _memberStrategyManager.RemoveMember(memberThatLeft);

        if (_metaMembers.TryGetValue(memberThatLeft.Id, out var meta))
        {
            _membersByIndex = _membersByIndex.Remove(meta.Index);

            if (_indexByAddress.TryGetValue(memberThatLeft.Address, out _))
            {
                _indexByAddress = _indexByAddress.Remove(memberThatLeft.Address);
            }

            _metaMembers = _metaMembers.Remove(memberThatLeft.Id);
        }
    }

    private void HandleMemberJoin(Member newMember)
    {
        try
        {
            if (_metaMembers.ContainsKey(newMember.Id))
            {
                Logger.LogError("Member {Member} already exists in MemberList", newMember);
                return;
            }

            var index = _nextMemberIndex++;
            _metaMembers = _metaMembers.SetItem(newMember.Id, new MetaMember(newMember, index));
            _membersByIndex = _membersByIndex.SetItem(index, newMember);
            _indexByAddress = _indexByAddress.SetItem(newMember.Address, index);

            _memberStrategyManager.AddMember(newMember);
        }
        catch (Exception x)
        {
            Logger.LogError(x, "Error during MemberJoin {Member}", newMember);
        }
    }

    private static void LogTopologyChanges(ClusterTopology topology)
    {
        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[MemberList] Published ClusterTopology event {ClusterTopology}", topology);
        }

        if (topology.Joined.Any())
        {
            Logger.ClusterMembersJoined(topology.Joined);
        }

        if (topology.Left.Any())
        {
            Logger.ClusterMembersLeft(topology.Left);
        }
    }

    private void TrySetStarted(ImmutableMemberSet activeMembers)
    {
        if (_startedTcs.Task.IsCompleted)
        {
            return;
        }

        if (_isClient || activeMembers.Contains(_system.Id))
        {
            _startedTcs.TrySetResult(true);
        }
    }

    private void SelfBlocked()
    {
        // If already shutting down, nothing to do.
        if (Stopping || _system.Shutdown.IsCancellationRequested)
        {
            return;
        }

        Logger.BlockedExiting(MemberId);
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
    }

    private void TerminateMember(Member memberThatLeft)
    {
        var endpointTerminated = new EndpointTerminatedEvent(true, memberThatLeft.Address, memberThatLeft.Id);

        if (Logger.IsEnabled(LogLevel.Debug))
        {
            Logger.LogDebug("[MemberList] Published event {@EndpointTerminated}", endpointTerminated);
        }

        _cluster.System.EventStream.Publish(endpointTerminated);
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
                Logger.LogError(x, "[MemberList] Failed to broadcast {MessagePayload} to {Pid}", message, pid);
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