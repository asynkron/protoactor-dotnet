// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

namespace Proto.Cluster
{
    public class GossipActor : IActor
    {
        private long _localSequenceNo;
        private GossipState _state = new();
        private readonly Random _rnd = new();

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

            Console.WriteLine($"{context.System.Id} got new state: {newState} ... old state: {_state}");
            
            _state = newState;
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
                var pid = PID.FromAddress(member.Address, Gossip.GossipActorName);
                context.Send(pid, _state);
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