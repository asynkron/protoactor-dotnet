// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Cluster.Gossip
{
    public class GossipActor : IActor
    {
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();
        private readonly Dictionary<string, long> _watermarks = new();
        private uint _clusterTopologyHash;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState => OnSetGossipStateKey(context, setState),
            GossipState remoteState    => OnGossipState(context, remoteState),
            SendGossipState            => OnSendGossipState(context),
            _                          => Task.CompletedTask
        };

        private async Task OnGossipState(IContext context, GossipState remoteState)
        {
            if (!GossipStateManagement.MergeState(_state, remoteState, out var newState))
            {
                return;
            }
            _state = newState;
            
            // Console.WriteLine();
            // Console.WriteLine();
           // Console.WriteLine($"{context.System.Id} got new state: {remoteState}");
            // Console.WriteLine();
            // Console.WriteLine();


            var allMembers = context.System.Cluster().MemberList.GetMembers();
            
            var(consensus, hash)= GossipStateManagement.ElectLeader(_state,context.System.Id, allMembers);

            if (consensus)
            {
                if (hash != _clusterTopologyHash)
                {
                    _clusterTopologyHash = hash;
                    Console.WriteLine($"CONSSENSUS {context.System.Id} - {_clusterTopologyHash}");
                }
            }
            else
            {
             //   Console.WriteLine(_state);
            }
        }
        
        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            GossipStateManagement.SetKey(_state, setStateKey.Key,setStateKey.Value  , context.System.Id, ref _localSequenceNo);

            return Task.CompletedTask;
        }

        private Task OnSendGossipState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetOtherMembers();
            var fanOutMembers = PickRandomFanOutMembers(members,3);

            foreach (var member in fanOutMembers)
            {
                var stateForMember = /*_state; */GossipStateManagement.FilterGossipStateForMember(_state, _watermarks,member.Id);
                var pid = PID.FromAddress(member.Address, Gossiper.GossipActorName);
                context.Send(pid, stateForMember);
            }

            return Task.CompletedTask;
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