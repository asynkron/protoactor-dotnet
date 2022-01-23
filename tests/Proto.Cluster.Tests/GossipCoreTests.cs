// -----------------------------------------------------------------------
// <copyright file="GossipCoreTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Xunit;
using Proto.Cluster;

namespace Proto.Cluster.Tests
{
    public class GossipCoreTests
    {
        [Fact]
        public void Foo()
        {
            const int memberCount = 10;
            const int fanout = 3;

            var members =
                Enumerable
                    .Range(0, memberCount)
                    .Select(_ => new Member() {Id = Guid.NewGuid().ToString("N")})
                    .ToList();

            var environment =
                members
                    .ToDictionary(
                        m => m.Id, 
                        m => (Gossip: new Gossip.Gossip(m.Id, fanout, () => ImmutableHashSet<string>.Empty, null),
                                Member: m));

            void SendState(MemberStateDelta memberStateDelta, Member targetMember, InstanceLogger _)
            {
                var target = environment[targetMember.Id];
                target.Gossip.ReceiveState(memberStateDelta.State);
                memberStateDelta.CommitOffsets();
            }

            var topology = new ClusterTopology()
            {
                TopologyHash = Member.TopologyHash(members),
                Members = { members }
            };

            foreach (var m in environment.Values)
            {
                m.Gossip.UpdateClusterTopology(topology.Clone());
            }

            var first = environment.Values.First().Gossip;

            //TODO: how do I create a consensus check and check for topology consensus now?
            //w/o using Gossiper to run via actor infra that is...

            foreach (var m in environment.Values)
            {
                m.Gossip.SendState(SendState);
            }
            
        }
    }
}