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
    public class GossipFoo
    {
        private static readonly ILogger Logger = Log.CreateLogger<GossipFoo>();
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

        public GossipFoo(string myId, Func<ImmutableHashSet<string>> getBlockedMembers, InstanceLogger? logger, int gossipFanout)
        {
            _myId = myId;
            _getBlockedMembers = getBlockedMembers;
            _logger = logger;
            _gossipFanout = gossipFanout;
        }

        public Task OnClusterTopology(ClusterTopology clusterTopology)
        {
            _otherMembers = clusterTopology.Members.Where(m => m.Id != _myId).ToArray();
            _activeMemberIds = clusterTopology.Members.Select(m => m.Id).ToImmutableHashSet();
            SetState("topology", clusterTopology);
            CheckConsensus("topology");
            return Task.CompletedTask;
        }

        internal Task OnAddConsensusCheck(AddConsensusCheck msg)
        {
            _consensusChecks.Add(msg.Check);

            // Check when adding, if we are already consistent
            msg.Check.Check(_state, _activeMemberIds);
            return Task.CompletedTask;
        }

        internal Task OnRemoveConsensusCheck(RemoveConsensusCheck request)
        {
            _consensusChecks.Remove(request.Id);
            return Task.CompletedTask;
        }

        public GetGossipStateResponse OnGetGossipStateKey(GetGossipStateRequest getState)
        {
            var entries = ImmutableDictionary<string, Any>.Empty;
            var key = getState.Key;

            foreach (var (memberId, memberState) in _state.Members)
            {
                if (memberState.Values.TryGetValue(key, out var value))
                {
                    entries = entries.SetItem(memberId, value.Value);
                }
            }

            return new GetGossipStateResponse(entries);
        }

        public Member? TryGetSenderMember(string senderAddress)
        {
            if (senderAddress is null) return default;

            return Array.Find(_otherMembers, member => member.Address.Equals(senderAddress, StringComparison.OrdinalIgnoreCase));
        }

        public IReadOnlyCollection<GossipUpdate> ReceiveState(GossipState remoteState)
        {
            var updates = GossipStateManagement.MergeState(_state, remoteState, out var newState, out var updatedKeys);

            if (updates.Count <= 0) return updates;

            _state = newState;
            CheckConsensus(updatedKeys);
            return updates;
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

        public Task<SetGossipStateResponse> OnSetGossipStateKey(SetGossipStateKey setStateKey)
        {
            var logger = _logger?.BeginMethodScope();

            var (key, message) = setStateKey;
            SetState(key, message);
            logger?.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);
            Logger.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);

            if (!_state.Members.ContainsKey(_myId))
            {
                logger?.LogCritical("State corrupt");
            }

            CheckConsensus(setStateKey.Key);

            return Task.FromResult(new SetGossipStateResponse());
        }

        private long SetState(string key, IMessage message)
            => _localSequenceNo = GossipStateManagement.SetKey(_state, key, message, _myId, _localSequenceNo);

        public Task<SendGossipStateResponse> OnSendGossipState(Action<Member, InstanceLogger?> sendGossipForMember)
        {
            var logger = _logger?.BeginMethodScope();

            PurgeBannedMembers();

            foreach (var member in _otherMembers)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }

            var fanOutMembers = PickRandomFanOutMembers(_otherMembers, _gossipFanout);

            foreach (var member in fanOutMembers)
            {
                //fire and forget, we handle results in ReenterAfter
                sendGossipForMember(member, logger);
            }

            return Task.FromResult(new SendGossipStateResponse());
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

        public bool TryGetStateForMember(Member member, out ImmutableDictionary<string, long> pendingOffsets, out GossipState stateForMember)
        {
            (pendingOffsets, stateForMember) = GossipStateManagement.FilterGossipStateForMember(_state, _committedOffsets, member.Id);

            //if we dont have any state to send, don't send it...
            return pendingOffsets != _committedOffsets;
        }

        public void CommitPendingOffsets(ImmutableDictionary<string, long> pendingOffsets)
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

        private List<Member> PickRandomFanOutMembers(Member[] members, int fanOutBy) =>
            members
                .Select(m => (member: m, index: _rnd.Next()))
                .OrderBy(m => m.index)
                .Take(fanOutBy)
                .Select(m => m.member)
                .ToList();
    }
}