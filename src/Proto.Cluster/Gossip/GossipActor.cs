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
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto.Logging;

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

        public GossipActor(TimeSpan gossipRequestTimeout) => _gossipRequestTimeout = gossipRequestTimeout;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState     => OnSetGossipStateKey(context, setState),
            GetGossipStateRequest getState => OnGetGossipStateKey(context, getState),
            GossipRequest gossipRequest    => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest         => OnSendGossipState(context),
            _                              => Task.CompletedTask
        };

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
            var updates = GossipStateManagement.MergeState(_state, remoteState, out var newState);

            if (updates.Any())
            {
                foreach (var update in updates)
                {
                    context.System.EventStream.Publish(update);
                }

                _state = newState;
                CheckConsensus(context);
            }

            //Ack, we got it
            context.Respond(new GossipResponse());
            return Task.CompletedTask;
        }

        private void CheckConsensus(IContext context)
        {
            var allMembers = context.System.Cluster().MemberList.GetMembers();

            var (consensus, hash) = GossipStateManagement.CheckConsensus(context, _state, context.System.Id, allMembers);

            if (!consensus)
            {
                context.Cluster().MemberList.TryResetTopologyConsensus();
                return;
            }

            context.Cluster().MemberList.TrySetTopologyConsensus();
        }

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            var logger = context.Logger()?.BeginMethodScope();

            GossipStateManagement.SetKey(_state, setStateKey.Key, setStateKey.Value, context.System.Id, ref _localSequenceNo);
            logger?.LogDebug("Setting state key {Key} - {Value} - {State}", setStateKey.Key, setStateKey.Value, _state);
            Logger.LogDebug("Setting state key {Key} - {Value} - {State}", setStateKey.Key, setStateKey.Value, _state);
            if (!_state.Members.ContainsKey(context.System.Id))
            {
                logger?.LogCritical("State corrupt");
            }

            return Task.CompletedTask;
        }

        private Task OnSendGossipState(IContext context)
        {
            var logger = context.Logger()?.BeginMethodScope();
            var members = context.System.Cluster().MemberList.GetOtherMembers();

            PurgeBannedMembers(context);

            foreach (var member in members)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }

            var fanOutMembers = PickRandomFanOutMembers(members, context.System.Cluster().Config.GossipFanout);

            foreach (var member in fanOutMembers)
            {
                //fire and forget, we handle results in ReenterAfter
                SendGossipForMember(context, member, logger);
            }

            CheckConsensus(context);
            context.Respond(new SendGossipStateResponse());
            return Task.CompletedTask;
        }

        private void PurgeBannedMembers(IContext context)
        {
            var banned = context.Cluster().MemberList.BannedMembers;

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

            context.ReenterAfter(t, async tt => {
                    try
                    {
                        await tt;

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
            );
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