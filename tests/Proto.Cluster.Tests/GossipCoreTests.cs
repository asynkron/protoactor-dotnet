// -----------------------------------------------------------------------
// <copyright file="GossipCoreTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Xunit;
using Proto.Cluster;
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
        public async Task Foo()
        {
            const int memberCount = 200;
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
           
            
            var handle = RegisterConsensusCheck<ClusterTopology, ulong>("topology", topology => topology.TopologyHash, first);

            var gossipCount = 0l;
            var ct = CancellationTokens.FromSeconds(10);
            _ = Task.Run(async () => {
                    while (!ct.IsCancellationRequested)
                    {
                        Interlocked.Increment(ref gossipCount);
                        //emulate gossip requests
                        await Task.Delay(100);
                        foreach (var m in environment.Values)
                        {
                            m.Gossip.SendState(SendState);
                        }
                    }
                }
            ,ct);

            var x = await handle.TryGetConsensus(ct);
            var count = Interlocked.Read(ref gossipCount);
      
            _output.WriteLine("Consensus topology hash " + x.value);
            _output.WriteLine("Gossip count " + count);
            x.consensus.Should().BeTrue();

        }
        
        private IConsensusHandle<TV> RegisterConsensusCheck<T, TV>(string key, Func<T, TV?> getValue, IGossipConsensusChecker checker) where T : IMessage, new()
            => RegisterConsensusCheck(Gossiper.ConsensusCheckBuilder<TV>.Create(key, getValue),checker);
        
        private IConsensusHandle<T> RegisterConsensusCheck<T>(Gossiper.ConsensusCheckBuilder<T> builder, IGossipConsensusChecker checker)
            => RegisterConsensusCheck(builder.Check, builder.AffectedKeys, checker);
        
        private IConsensusHandle<T> RegisterConsensusCheck<T>(ConsensusCheck<T> hasConsensus, string[] affectedKeys, IGossipConsensusChecker checker)
        {
            var id = Guid.NewGuid().ToString("N");
            var handle = new GossipConsensusHandle<T>(() => checker.RemoveConsensusCheck(id));

            var check = new ConsensusCheck(id, CheckConsensus, affectedKeys);
            checker.AddConsensusCheck(check);

            return handle;

            void CheckConsensus(GossipState state, IImmutableSet<string> members)
            {
                var (consensus, value) = hasConsensus(state, members);

                if (consensus)
                {
                    handle.TrySetConsensus(value!);
                }
                else
                {
                    handle.TryResetConsensus();
                }
            }
        }
    }
}