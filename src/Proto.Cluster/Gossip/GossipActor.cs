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

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();
        private ImmutableDictionary<string, long> _committedOffsets = ImmutableDictionary<string, long>.Empty;
        private uint _clusterTopologyHash;

        public Task ReceiveAsync(IContext context)
        {
            return context.Message switch
            {
                SetGossipStateKey setState  => OnSetGossipStateKey(context, setState),
                GossipRequest gossipRequest => OnGossipRequest(context, gossipRequest),
                SendGossipStateRequest      => OnSendGossipState(context),
                _                           => Task.CompletedTask
            };
        }

        private async Task OnGossipRequest(IContext context, GossipRequest gossipRequest)
        {
            GossipState remoteState = gossipRequest.State;
            //Console.WriteLine("Got gossip request");
            //Ack, we got it

            if (GossipStateManagement.MergeState(_state, remoteState, out var newState))
            {
                _state = newState;

                var allMembers = context.System.Cluster().MemberList.GetMembers();

                var (consensus, hash) = GossipStateManagement.ElectLeader(_state, context.System.Id, allMembers);

                if (consensus)
                {
                    if (hash != _clusterTopologyHash)
                    {
                        _clusterTopologyHash = hash;
                        
                     //   Console.WriteLine($"CONSSENSUS {context.System.Id} - {_clusterTopologyHash}");
                    }
                }
            }
            // Console.WriteLine("Gossip request done..");
            context.Respond(new GossipResponse());
        }
        
        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            GossipStateManagement.SetKey(_state, setStateKey.Key,setStateKey.Value  , context.System.Id, ref _localSequenceNo);

            return Task.CompletedTask;
        }

        private async Task OnSendGossipState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetOtherMembers();

            // foreach (Member m in members)
            // {
            //     //if we know about members that are not yet in our 
            //     GossipStateManagement.EnsureMemberStateExists(_state, m.Id);
            // }
            var fanOutMembers = PickRandomFanOutMembers(members, 3);

            foreach (var member in fanOutMembers)
            {
                try
                {
                    var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);
                    var (pendingOffsets, stateForMember) = GossipStateManagement.FilterGossipStateForMember(_state, _committedOffsets, member.Id);

                    await context.RequestAsync<GossipResponse>(pid, new GossipRequest
                        {
                            State = stateForMember,
                        }, CancellationTokens.WithTimeout(500)
                    );

                    //only commit offsets if successful
                    _committedOffsets = pendingOffsets;
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
                    //TODO: log
                    Console.WriteLine(x);
                }
            }
            
            context.Respond(new SendGossipStateResponse());
        }

        private List<Member>? PickRandomFanOutMembers(Member[] members, int fanOutBy) => 
            members
            .Select(m => (member: m, index: _rnd.Next()))
            .OrderBy(m => m.index)
            .Take(fanOutBy)
            .Select(m => m.member)
            .ToList();
    }
}