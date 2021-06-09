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
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<GossipActor>();
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();
        private ImmutableDictionary<string, long> _committedOffsets = ImmutableDictionary<string, long>.Empty;
        private ulong _clusterTopologyHash;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState  => OnSetGossipStateKey(context, setState),
            GossipRequest gossipRequest => OnGossipRequest(context, gossipRequest),
            SendGossipStateRequest      => OnSendGossipState(context),
            _                           => Task.CompletedTask
        };

        private Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            var remoteState = gossipRequest.State;
            //Ack, we got it
            if (GossipStateManagement.MergeState(_state, remoteState, out var newState))
            {
                _state = newState;
                CheckConsensus(context);
            }

            var response = new GossipResponse();
            context.Respond(response);
            return Task.CompletedTask;
        }

        private void CheckConsensus(IContext context)
        {
            var allMembers = context.System.Cluster().MemberList.GetMembers();

            var (consensus, hash) = GossipStateManagement.CheckConsensus(context, _state, context.System.Id, allMembers);

            if (!consensus)
            {
                context.Logger()?.LogDebug("No consensus {MemberId} - {State} - ", context.System.Id, _state);
                context.Cluster().MemberList.TryResetTopologyConsensus();
                return;
            }

            if (hash != _clusterTopologyHash)
            {
                //safe to call many times
                context.Cluster().MemberList.TrySetTopologyConsensus();
                
                context.Logger()?.LogDebug("Consensus {MemberId} - {TopologyHash} - {State}", context.System.Id, hash, _state);
                //reached consensus
                _clusterTopologyHash = hash;
            }

        }

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            GossipStateManagement.SetKey(_state, setStateKey.Key,setStateKey.Value  , context.System.Id, ref _localSequenceNo);
            return Task.CompletedTask;
        }

        private async Task OnSendGossipState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetOtherMembers();

            foreach (var member in members)
            {
                GossipStateManagement.EnsureMemberStateExists(_state, member.Id);
            }
            
            var fanOutMembers = PickRandomFanOutMembers(members, context.System.Cluster().Config.GossipFanout);

            foreach (var member in fanOutMembers)
            {
                try
                {
                    var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);
                    var (pendingOffsets, stateForMember) = GossipStateManagement.FilterGossipStateForMember(_state, _committedOffsets, member.Id);

                    //if we dont have any state to send, don't send it...
                    if (pendingOffsets == _committedOffsets)
                    {
                        continue;
                    }

                    //a short timeout is massively important, we cannot afford hanging around waiting for timeout, blocking other gossips from getting through
                    await context.RequestAsync<GossipResponse>(pid, new GossipRequest
                        {
                            State = stateForMember,
                        }, CancellationTokens.WithTimeout(500)
                    );

                    //only commit offsets if successful
                    _committedOffsets = pendingOffsets;

                    //update our state with the data from the remote node
                    //TODO: this needs to be improved with filter state on sender side, and then Ack from here
                    // GossipStateManagement.MergeState(_state, response.State, out var newState);
                    // _state = newState;
                }
                catch (DeadLetterException)
                {
                    
                }
                catch (OperationCanceledException)
                {
                    
                }
                catch (TimeoutException)
                {
                    
                }
                catch(Exception x)
                {
                    Logger.LogError(x, "OnSendGossipState failed");
                }
            }
            
            CheckConsensus(context);
            context.Respond(new SendGossipStateResponse());
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