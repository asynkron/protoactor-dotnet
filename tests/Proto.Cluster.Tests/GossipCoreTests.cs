// -----------------------------------------------------------------------
// <copyright file="GossipCoreTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class GossipCoreTests
    {
        private readonly ITestOutputHelper _output;

        public GossipCoreTests(ITestOutputHelper output)
        {
            _output = output;
        }
        
        [Fact]
        public async Task Large_cluster_should_get_topology_consensus()
        {
            const int memberCount = 100;
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
                        m => (Gossip: new Gossip.Gossip(m.Id, fanout, memberCount, () => ImmutableHashSet<string>.Empty, null),
                                Member: m));

            var sends = 0L;
            void SendState(MemberStateDelta memberStateDelta, Member targetMember, InstanceLogger _)
            {
                Interlocked.Increment(ref sends);
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
                await m.Gossip.UpdateClusterTopology(topology.Clone());
            }

            var first = environment.Values.First().Gossip;

            var checkDefinition = Gossiper.ConsensusCheckBuilder<ulong>.Create(GossipKeys.Topology, (ClusterTopology tp) => tp.TopologyHash);
            var id = Guid.NewGuid().ToString();
            var (handle, check) = checkDefinition.Build(() => first.RemoveConsensusCheck(id));
            first.AddConsensusCheck(id, check);

            var gossipGenerations = 0L;
            var ct = CancellationTokens.FromSeconds(10);
            _ = Task.Run(() => {
                    while (!ct.IsCancellationRequested)
                    {
                        // ReSharper disable once AccessToModifiedClosure
                        Interlocked.Increment(ref gossipGenerations);
                        foreach (var m in environment.Values)
                        {
                            m.Gossip.SendState(SendState);
                        }
                    }
                }
            ,ct);

            var x = await handle.TryGetConsensus(ct);

            _output.WriteLine("Consensus topology hash " + x.value);
            _output.WriteLine("Gossip generations " + Interlocked.Read(ref gossipGenerations));
            _output.WriteLine("Send count " + Interlocked.Read(ref sends));
            x.consensus.Should().BeTrue();
        }
    }
}