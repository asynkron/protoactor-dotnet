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
            var res = await Pid.RequestAsync<MembersByKindResponse>(new MembersByKindRequest(kind, true));
            return res.Kinds;
        }

        public static async Task<string> GetMemberByDHTAsync(string name, string kind)
        {
            var res = await Pid.RequestAsync<MemberResponse>(new MemberByDHTRequest(name, kind));
            return res.Address;
        }

        public static async Task<string> GetMemberByRoundRobinAsync(string kind)
        {
            var res = await Pid.RequestAsync<MemberResponse>(new MemberByRoundRobinRequest(kind));
            return res.Address;
        }
    }

    internal class MembersByKindResponse
    {
        public MembersByKindResponse(string[] kinds)
        {
            Kinds = kinds ?? throw new ArgumentNullException(nameof(kinds));
        }

        public string[] Kinds { get; set; }
    }

    internal class MembersByKindRequest
    {
        public MembersByKindRequest(string kind, bool onlyAlive)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
            OnlyAlive = onlyAlive;
        }

        public string Kind { get; }
        public bool OnlyAlive { get; }
    }

    internal class MemberResponse
    {
        public MemberResponse(string address)
        {
            Address = address ?? throw new ArgumentNullException(nameof(address));
        }

        public string Address { get; }
    }

    internal class MemberByDHTRequest
    {
        public MemberByDHTRequest(string name, string kind)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public string Name { get; }
        public string Kind { get; }
    }

    internal class MemberByRoundRobinRequest
    {
        public MemberByRoundRobinRequest(string kind)
        {
            Kind = kind ?? throw new ArgumentNullException(nameof(kind));
        }

        public string Kind { get; }
    }
}