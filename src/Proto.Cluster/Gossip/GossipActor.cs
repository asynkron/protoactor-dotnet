// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
using Proto.Remote;

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private readonly TimeSpan _gossipRequestTimeout;
        private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();
        private ImmutableDictionary<string, long> _committedOffsets = ImmutableDictionary<string, long>.Empty;
        private ImmutableHashSet<string> _activeMemberIds = ImmutableHashSet<string>.Empty;
        private Member[] _otherMembers = Array.Empty<Member>();
        private readonly ConsensusChecks _consensusChecks = new();

        // lookup from state key -> consensus checks

        public GossipActor(TimeSpan gossipRequestTimeout) => _gossipRequestTimeout = gossipRequestTimeout;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState      => OnSetGossipStateKey(context, setState),
            GetGossipStateRequest getState  => OnGetGossipStateKey(context, getState),
            GossipRequest gossipRequest     => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest          => OnSendGossipState(context),
            AddConsensusCheck request       => OnAddConsensusCheck(context, request),
            RemoveConsensusCheck request    => OnRemoveConsensusCheck(request),
            ClusterTopology clusterTopology => OnClusterTopology(context, clusterTopology),
            _                               => Task.CompletedTask
        };

        private Task OnClusterTopology(IContext context, ClusterTopology clusterTopology)
        {
            _otherMembers = clusterTopology.Members.Where(m => m.Id != context.System.Id).ToArray();
            _activeMemberIds = clusterTopology.Members.Select(m => m.Id).ToImmutableHashSet();
            SetState(context, "topology", clusterTopology);
            CheckConsensus(context, "topology");
            return Task.CompletedTask;
        }

        private Task OnAddConsensusCheck(IContext context, AddConsensusCheck msg)
        {
            _consensusChecks.Add(msg.Check);

            // Check when adding, if we are already consistent
            msg.Check.Check(_state, _activeMemberIds, context);
            return Task.CompletedTask;
        }

        private Task OnRemoveConsensusCheck(RemoveConsensusCheck request)
        {
            _consensusChecks.Remove(request.Id);
            return Task.CompletedTask;
        }

        private Task OnGetGossipStateKey(IContext context, GetGossipStateRequest getState)
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

            var res = new GetGossipStateResponse(entries);
            context.Respond(res);
            return Task.CompletedTask;
        }

        private Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            var logger = context.Logger()?.BeginScope<GossipActor>();
            logger?.LogDebug("Gossip Request {Sender}", context.Sender!);
            Logger.LogDebug("Gossip Request {Sender}", context.Sender!);
            var remoteState = gossipRequest.State;
            var updates = GossipStateManagement.MergeState(_state, remoteState, out var newState, out var updatedKeys);

            if (updates.Count > 0)
            {
                foreach (var update in updates)
                {
                    context.System.EventStream.Publish(update);
                }

                _state = newState;
                CheckConsensus(context, updatedKeys);
            }

            //Ack, we got it
            context.Respond(new GossipResponse());
            return Task.CompletedTask;
        }

        private void CheckConsensus(IContext context, string updatedKey)
        {
            foreach (var consensusCheck in _consensusChecks.GetByUpdatedKey(updatedKey))
            {
                consensusCheck.Check(_state, _activeMemberIds, context);
            }
        }

        private void CheckConsensus(IContext context, IEnumerable<string> updatedKeys)
        {
            foreach (var consensusCheck in _consensusChecks.GetByUpdatedKeys(updatedKeys))
            {
                consensusCheck.Check(_state, _activeMemberIds, context);
            }
        }

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var logger = context.Logger()?.BeginMethodScope();

            var (key, message) = setStateKey;
            SetState(context, key, message);
            logger?.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);
            Logger.LogDebug("Setting state key {Key} - {Value} - {State}", key, message, _state);

            if (!_state.Members.ContainsKey(context.System.Id))
            {
                logger?.LogCritical("State corrupt");
            }

            CheckConsensus(context, setStateKey.Key);

            if (context.Sender is not null)
            {
                context.Respond(new SetGossipStateResponse());
            }

            return Task.CompletedTask;
        }

        private long SetState(IContext context, string key, IMessage message)
            => _localSequenceNo = GossipStateManagement.SetKey(_state, key, message, context.System.Id, _localSequenceNo);

        private Task OnSendGossipState(IContext context)
        {
            var logger = context.Logger()?.BeginMethodScope();

            PurgeBannedMembers(context);

            foreach (var member in _otherMembers)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }

            var fanOutMembers = PickRandomFanOutMembers(_otherMembers, context.System.Cluster().Config.GossipFanout);

            foreach (var member in fanOutMembers)
            {
                //fire and forget, we handle results in ReenterAfter
                SendGossipForMember(context, member, logger);
            }

            // CheckConsensus(context);
            context.Respond(new SendGossipStateResponse());
            return Task.CompletedTask;
        }

        private void PurgeBannedMembers(IContext context)
        {
            var banned = context.Remote().BlockList.BlockedMembers;

            foreach (var memberId in _state.Members.Keys.ToArray())
            {
                if (banned.Contains(memberId))
                {
                    _state.Members.Remove(memberId);
                }
            }
        }

        private void SendGossipForMember(IContext context, Member member, InstanceLogger? logger)
        {
            var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);
            var (pendingOffsets, stateForMember) = GossipStateManagement.FilterGossipStateForMember(_state, _committedOffsets, member.Id);

            //if we dont have any state to send, don't send it...
            if (pendingOffsets == _committedOffsets)
            {
                return;
            }

            logger?.LogInformation("Sending GossipRequest to {MemberId}", member.Id);
            Logger.LogDebug("Sending GossipRequest to {MemberId}", member.Id);

            //a short timeout is massively important, we cannot afford hanging around waiting for timeout, blocking other gossips from getting through
            var t = context.RequestAsync<GossipResponse>(pid, new GossipRequest
                {
                    State = stateForMember,
                }, CancellationTokens.WithTimeout(_gossipRequestTimeout)
            );

            context.ReenterAfter(t, async task => await GossipReenter(context, task, pendingOffsets));
        }

        private async Task GossipReenter(IContext context, Task<GossipResponse> task, ImmutableDictionary<string, long> pendingOffsets)
        {
            var logger = context.Logger();

            try
            {
                await task;

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
            catch (DeadLetterException)
            {
                logger?.LogWarning("DeadLetter");
            }
            catch (OperationCanceledException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (TimeoutException)
            {
                logger?.LogWarning("Timeout");
            }
            catch (Exception x)
            {
                logger?.LogError(x, "OnSendGossipState failed");
                Logger.LogError(x, "OnSendGossipState failed");
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