// -----------------------------------------------------------------------
//   <copyright file="MemberList.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class MemberList
    {
        public static PID Pid { get; private set; }

        private static Subscription<object> clusterTopologyEvnSub;
        
        internal static void SubscribeToEventStream()
        {
            clusterTopologyEvnSub = Actor.EventStream.Subscribe<ClusterTopologyEvent>(Pid.Tell);
        }

        internal static void UnsubEventStream()
        {
            Actor.EventStream.Unsubscribe(clusterTopologyEvnSub.Id);
        }

        internal static void Spawn()
        {
            Pid = Actor.SpawnNamed(Actor.FromProducer(() => new MemberListActor()), "memberlist");
        }

        internal static void Stop()
        {
            Pid.Stop();
        }

        public static async Task<string[]> GetMembersAsync(string kind)
        {
            //if there are no nodes holding the requested kind, just wait
            while (true)
            {
                var res = await Pid.RequestAsync<MemberByKindResponse>(new MemberByKindRequest(kind, true));
                if (res.Kinds.Any())
                {
                    return res.Kinds;
                }
                await Task.Delay(500);
            }
        }

        public static async Task<string> GetMemberAsync(string name, string kind)
        {
            var members = await GetMembersAsync(kind);
            var hdv = new Rendezvous(members);
            var member = hdv.GetNode(name);
            return member;
        }
    }

    internal class MemberByKindResponse
    {
        public MemberByKindResponse(string[] kinds)
        {
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
        }

        public string[] Kinds { get; set; }
    }

    internal class MemberByKindRequest
    {
        public MemberByKindRequest(string kind, bool onlyAlive)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            OnlyAlive = onlyAlive;
        }

        public string Kind { get; }
        public bool OnlyAlive { get; }
    }
}