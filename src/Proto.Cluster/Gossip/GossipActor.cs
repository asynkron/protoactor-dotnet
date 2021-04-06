// -----------------------------------------------------------------------
// <copyright file="GossipActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;

namespace Proto.Cluster
{
    public class GossipActor : IActor
    {
        private int _localSequenceNo = 0;
        private GossipState _state = new();

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            SetGossipStateKey setState => OnSetGossipStateKey(context, setState),
            GossipState remoteState    => OnGossipState(context, remoteState),
            SendGossipState            => OnSendGossipState(context),
            // HeartbeatRequest           => OnHeartbeatRequest(context),
            _ => Task.CompletedTask
        };

        // private static Task OnHeartbeatRequest(IContext context)
        // {
        //     context.Respond(new HeartbeatResponse
        //         {
        //             ActorCount = (uint) context.System.ProcessRegistry.ProcessCount
        //         }
        //     );
        //     return Task.CompletedTask;
        // }

        private async Task OnGossipState(IContext context, GossipState remoteState)
        {
            var (dirty, newState) = GossipStateManagement.MergeState(_state, remoteState);
            
            if (!dirty)
                return;
            
            Console.WriteLine($"{context.System.Id} got new state: {newState} ... old state: {_state}");
            
            _state = newState;
            // await GossipMyState(context);
        }

        

        private Task OnSetGossipStateKey(IContext context, SetGossipStateKey setStateKey)
        {
            SetKey(context, setStateKey);

            return Task.CompletedTask;
            //  await GossipMyState(context);
        }

        private void SetKey(IContext context, SetGossipStateKey setStateKey)
        {
            var memberId = context.System.Id;
            var (key, value) = setStateKey;

            //if entry does not exist, add it
            var memberState = GossipStateManagement.EnsureMemberStateExists(_state, memberId);
            var entry = GossipStateManagement.EnsureEntryExists(memberState, key);

            _localSequenceNo++;

            entry.SequenceNumber = _localSequenceNo;
            entry.Value = Any.Pack(value);
        }

        private async Task OnSendGossipState(IContext context)
        {
            var members = context.System.Cluster().MemberList.GetOtherMembers();

            var rnd = new Random();
            var gossipToMembers =
                members
                    .Select(m => (member:m, index:rnd.Next()))
                    .OrderBy(m => m.index)
                    .Take(3)
                    .Select(m => m.member)
                    .ToList();

            foreach (var member in gossipToMembers)
            {
                var pid = PID.FromAddress(member.Address, Gossip.GossipActorName);
                context.Send(pid, _state);
            }
        }
    }
}