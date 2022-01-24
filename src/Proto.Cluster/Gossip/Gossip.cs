// -----------------------------------------------------------------------
// <copyright file="GossipFoo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    internal class Gossip
        : IGossip
    {
        private static readonly ILogger Logger = Log.CreateLogger<Gossip>();
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();
        private ImmutableDictionary<string, long> _committedOffsets = ImmutableDictionary<string, long>.Empty;
        private ImmutableHashSet<string> _activeMemberIds = ImmutableHashSet<string>.Empty;
        private Member[] _otherMembers = Array.Empty<Member>();
        private readonly ConsensusChecks _consensusChecks = new();
        private readonly string _myId;
        private readonly Func<ImmutableHashSet<string>> _getBlockedMembers;
        private readonly InstanceLogger? _logger;
        private readonly int _gossipFanout;
        private readonly int _gossipMaxSend;

        public Gossip(string myId, int gossipFanout, int gossipMaxSend, Func<ImmutableHashSet<string>> getBlockedMembers, InstanceLogger? logger)
        {
            _myId = myId;
            _getBlockedMembers = getBlockedMembers;
            _logger = logger;
            _gossipFanout = gossipFanout;
            _gossipMaxSend = gossipMaxSend;
        }

        public Task UpdateClusterTopology(ClusterTopology clusterTopology)
        {
            _otherMembers = clusterTopology.Members.Where(m => m.Id != _myId).ToArray();
            _activeMemberIds = clusterTopology.Members.Select(m => m.Id).ToImmutableHashSet();
            SetState("topology", clusterTopology);
            return Task.CompletedTask;
        }

        public void AddConsensusCheck(ConsensusCheck check)
        {
            _consensusChecks.Add(check);

            // Check when adding, if we are already consistent
            check.Check(_state, _activeMemberIds);
        }

        public void RemoveConsensusCheck(string id) => 
            _consensusChecks.Remove(id);

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

            if (updates.Count == 0) return ImmutableList<GossipUpdate>.Empty;

            _state = newState;
            CheckConsensus(updatedKeys);
            return updates.ToImmutableList();
        }

        private void CheckConsensus(string updatedKey)
        {
            foreach (var consensusCheck in _consensusChecks.GetByUpdatedKey(updatedKey))
            {
                consensusCheck.Check(_state, _activeMemberIds);
            }
        }

        private void CheckConsensus(IEnumerable<string> updatedKeys)
        {
            foreach (var consensusCheck in _consensusChecks.GetByUpdatedKeys(updatedKeys))
            {
                consensusCheck.Check(_state, _activeMemberIds);
            }
        }

        public void SetState(string key, IMessage message)
        {
            var logger = _logger?.BeginMethodScope();
            _localSequenceNo = GossipStateManagement.SetKey(_state, key, message, _myId, _localSequenceNo);
            logger?.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);
            Logger.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);

            if (!_state.Members.ContainsKey(_myId))
            {
                logger?.LogCritical("State corrupt");
            }

            CheckConsensus(key);
        }

        //TODO: this does not need to use a callback, it can return a list of MemberStates
        public void SendState(SendStateAction stateActionToMember)
        {
            var logger = _logger?.BeginMethodScope();

            PurgeBannedMembers();

            foreach (var member in _otherMembers)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }

            var randomMembers = _otherMembers.OrderByRandom(_rnd);

            var fanoutCount = 0;
            foreach (var member in randomMembers)
            {
                var memberState = GetMemberStateDelta(member.Id);
                if (!memberState.HasState)
                {
                    continue;
                }
                
                //fire and forget, we handle results in ReenterAfter
                stateActionToMember(memberState, member, logger);
                
                fanoutCount++;

                if (fanoutCount == _gossipFanout)
                {
                    break;
                }
            }
        }

        private void PurgeBannedMembers()
        {
            var banned = _getBlockedMembers();

            foreach (var memberId in _state.Members.Keys.ToArray())
            {
                if (banned.Contains(memberId))
                {
                    _state.Members.Remove(memberId);
                }
            }
        }

        public MemberStateDelta GetMemberStateDelta(string targetMemberId)
        {
            var newState = new GossipState();

            var count = 0;
            var pendingOffsets = _committedOffsets;

            //for each member
            foreach (var (memberId, memberState1) in _state.Members.OrderByRandom(_rnd, m => m.Key == _myId))
            {
                //we dont need to send back state to the owner of the state
                if (memberId == targetMemberId)
                {
                    continue;
                }

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
                        continue;

                    if (value.SequenceNumber > newWatermark)
                        newWatermark = value.SequenceNumber;

                    newMemberState.Values.Add(key, value);
                }

                //don't send memberStates that we have no new data for 
                if (newMemberState.Values.Count > 0)
                {
                    newState.Members.Add(memberId, newMemberState);
                    pendingOffsets = pendingOffsets.SetItem(watermarkKey, newWatermark);
                }

                count++;

                if (count > _gossipMaxSend)
                {
                    break;
                }
            }

            //make sure to clone to make it a separate copy, avoid race conditions on mutate
            var hasState = _committedOffsets != pendingOffsets;
            var memberState = new MemberStateDelta(targetMemberId, hasState, newState, () => CommitPendingOffsets(pendingOffsets));
            return memberState;
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
}