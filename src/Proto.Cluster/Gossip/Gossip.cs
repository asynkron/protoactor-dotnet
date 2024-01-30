// -----------------------------------------------------------------------
// <copyright file="GossipFoo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------


/*
   Member abc state:
       GossipState
           MemberState abc
               Key123 - sequence id 1
               Key456 - sequence id 2
   
           MemberState def
               Key123 - sequence id 1
               Key456 - sequence id 2
   
       committed offsets
           "abc.def" 1
   
   Member def state:
       GossipState
           MemberState abc
               Key123 - sequence id 1
               Key456 - sequence id 2
   
           MemberState def
               Key123 - sequence id 3
               Key456 - sequence id 2
   
       committed offsets
           "abc.def" 2
   
   
   gossip from def to abc
   scan all entries for all member states _except_ for abc (we shouldn´t send their state to them)
   find all entries that are higher than the committed offset for the member state
   send all entries to the target member
   
   committed offsets is local per member, meaning we will send excessive and needless data to other members
   maybe this could be improved by having each nodes committed offsets as part of the MemberState (?)   
 */
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip;

internal class Gossip
    : IGossip
{
    private static readonly ILogger Logger = Log.CreateLogger<Gossip>();
    private readonly ConsensusChecks _consensusChecks = new();
    private readonly Func<ImmutableHashSet<string>> _getMembers;
    private readonly bool _gossipDebugLogging;
    private readonly int _gossipFanout;
    private readonly int _gossipMaxSend;
    private readonly InstanceLogger? _logger;
    private readonly string _myId;
    private readonly Random _rnd = new();
    private ImmutableHashSet<string> _activeMemberIds = ImmutableHashSet<string>.Empty;
    private ImmutableDictionary<string, long> _committedOffsets = ImmutableDictionary<string, long>.Empty;
    private long _localSequenceNo;
    private Member[] _otherMembers = Array.Empty<Member>();
    private GossipState _state = new();

    public Gossip(string myId, int gossipFanout, int gossipMaxSend, InstanceLogger? logger,
        Func<ImmutableHashSet<string>> getMembers, bool gossipDebugLogging)
    {
        _myId = myId;
        _logger = logger;
        _getMembers = getMembers;
        _gossipDebugLogging = gossipDebugLogging;
        _gossipFanout = gossipFanout;
        _gossipMaxSend = gossipMaxSend;
    }

    public Task UpdateClusterTopology(ClusterTopology clusterTopology)
    {
        //TODO: optimize
        _otherMembers = clusterTopology.Members.Where(m => m.Id != _myId).ToArray();
        _activeMemberIds = clusterTopology.Members.Select(m => m.Id).ToImmutableHashSet();
        SetState(GossipKeys.Topology, clusterTopology);

        return Task.CompletedTask;
    }

    public void AddConsensusCheck(string id, ConsensusCheck check)
    {
        _consensusChecks.Add(id, check);

        // Check when adding, if we are already consistent
        check.Check(_state, _activeMemberIds);
    }

    public void RemoveConsensusCheck(string id) => _consensusChecks.Remove(id);

    public GossipState GetStateSnapshot() => _state.Clone();

    public ImmutableDictionary<string, Any> GetState(string key)
    {
        var entries = ImmutableDictionary<string, Any>.Empty;

        foreach (var (memberId, memberState) in _state.Members)
        {
            if (memberState.Values.TryGetValue(key, out var value))
            {
                entries = entries.SetItem(memberId, value.Value);
            }
        }

        return entries;
    }

    public ImmutableList<GossipUpdate> ReceiveState(GossipState remoteState)
    {
        var updates = GossipStateManagement.MergeState(_state, remoteState, out var newState, out var updatedKeys);

        if (updates.Count == 0)
        {
            return ImmutableList<GossipUpdate>.Empty;
        }

        if (_gossipDebugLogging)
        {
            Logger.LogInformation("ReceiveState: Gossip updates {Updates}", updates);
            Logger.LogInformation("ReceiveState: Old Gossip state {State}", _state);
            Logger.LogInformation("ReceiveState: New Gossip state {State}", newState);
            Logger.LogInformation("ReceiveState: Updated Keys {Keys}", updatedKeys);
        }

        _state = newState;
        
        CheckConsensus(updatedKeys);

        return updates.ToImmutableList();
    }

    public void SetState(string key, IMessage message)
    {
        var logger = _logger?.BeginMethodScope();
        _localSequenceNo = GossipStateManagement.SetKey(_state, key, message, _myId, _localSequenceNo);
        logger?.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);
        Logger.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);
        
        if (_gossipDebugLogging)
        {
            Logger.LogInformation("SetState: Gossip key {Key} - {Value} - {State}", key, message, _state);
        }

        if (!_state.Members.ContainsKey(_myId))
        {
            Logger?.LogCritical("State corrupt");
            logger?.LogCritical("State corrupt");
        }

        CheckConsensus(key);
    }

    //TODO: this does not need to use a callback, it can return a list of MemberStates
    public void SendState(SendStateAction sendStateToMember)
    {
        try
        {
            var logger = _logger?.BeginMethodScope();

            foreach (var member in _otherMembers)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }

            var randomMembers = _otherMembers.OrderByRandom(_rnd).ToArray();

            var fanoutCount = 0;
            
            if (_gossipDebugLogging)
            {
                var ids = randomMembers.Select(m => m.Id).ToArray();
                Logger.LogInformation("SendState: Gossip to members {Members}", ids);
            }

            foreach (var member in randomMembers)
            {
                //TODO: we can chunk up sends here
                //instead of sending less state, we can send all of it, but in chunks
                var memberState = GetMemberStateDelta(member.Id);

                if (!memberState.HasState)
                {
                    continue;
                }

                //fire and forget, we handle results in ReenterAfter
                sendStateToMember(memberState, member, logger);

                fanoutCount++;

                if (fanoutCount == _gossipFanout)
                {
                    break;
                }
            }
        }
        catch (Exception x)
        {
            Logger.LogError(x, "SendState failed");
        }
    }

    public MemberStateDelta GetMemberStateDelta(string targetMemberId)
    {
        var newState = new GossipState();

        var count = 0;
        var pendingOffsets = _committedOffsets;

        //for each member
        var members = _state
            .Members
            .Where(m => m.Key != targetMemberId) //we dont need to send back state to the owner of the state
            .OrderByRandom(_rnd, m => m.Key == _myId);

        foreach (var (memberId, memberState1) in members)
        {
            //create an empty state
            var newMemberState = new GossipState.Types.GossipMemberState();

            var watermarkKey = $"{targetMemberId}.{memberId}";
            //get the watermark 
            _committedOffsets.TryGetValue(watermarkKey, out var watermark);
            var newWatermark = watermark;

            //for each value in member state
            foreach (var (key, value) in memberState1.Values)
            {
                if (value.SequenceNumber <= watermark)
                {
                    continue;
                }

                if (value.SequenceNumber > newWatermark)
                {
                    newWatermark = value.SequenceNumber;
                }

                newMemberState.Values.Add(key, value);
            }

            //don't send memberStates that we have no new data for 
            if (newMemberState.Values.Count > 0)
            {
                count++;
                newState.Members.Add(memberId, newMemberState);
                pendingOffsets = pendingOffsets.SetItem(watermarkKey, newWatermark);
            }

            if (count > _gossipMaxSend)
            {
                break;
            }
        }

        //make sure to clone to make it a separate copy, avoid race conditions on mutate
        var hasState = _committedOffsets != pendingOffsets;

        var memberState =
            new MemberStateDelta(targetMemberId, hasState, newState, () => CommitPendingOffsets(pendingOffsets));

        return memberState;
    }

    public ImmutableDictionary<string, GossipKeyValue> GetStateEntry(string key)
    {
        var entries = ImmutableDictionary<string, GossipKeyValue>.Empty;

        foreach (var (memberId, memberState) in _state.Members)
        {
            if (memberState.Values.TryGetValue(key, out var value))
            {
                entries = entries.SetItem(memberId, value);
            }
        }

        return entries;
    }

    private void CheckConsensus(string updatedKey)
    {
        //TODO: Optimize
        Purge();

        foreach (var consensusCheck in _consensusChecks.GetByUpdatedKey(updatedKey))
        {
            consensusCheck.Check(_state, _activeMemberIds);
        }
    }

    private void CheckConsensus(IEnumerable<string> updatedKeys)
    {
        //TODO: Optimize
        Purge();

        foreach (var consensusCheck in _consensusChecks.GetByUpdatedKeys(updatedKeys))
        {
            consensusCheck.Check(_state, _activeMemberIds);
        }
    }

    private void Purge()
    {
        //find all members that have sent topology
        var members = _getMembers();
        
        //TODO: what to do with _committedOffsets ?

        foreach (var memberId in _state.Members.Keys.ToArray())
        {
            if (!members.Contains(memberId))
            {
                _state.Members.Remove(memberId);
            }
        }
    }

    private void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets)
    {
        foreach (var (key, sequenceNumber) in pendingOffsets)
        {
            //TODO: this needs to be improved with filter state on sender side, and then Ack from here
            //update our state with the data from the remote node
            //GossipStateManagement.MergeState(_state, response.State, out var newState);
            //_state = newState;

            if (!_committedOffsets.ContainsKey(key) || _committedOffsets[key] < pendingOffsets[key])
            {
                _committedOffsets = _committedOffsets.SetItem(key, sequenceNumber);
            }
        }
    }
}