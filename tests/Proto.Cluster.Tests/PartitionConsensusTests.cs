using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class PartitionConsensusTests
{
    [Fact]
    public async Task PartitionedMember_ResumesGossipAfterDrop()
    {
        var fixture = new PartitionClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        await ClusterProbe.WaitForTopologyConsensusAsync(fixture.Members, TimeSpan.FromSeconds(5));

        var members = fixture.Members;
        var memberA = members[0];
        var memberB = members[1];
        var memberC = members[2];

        const string key = "test-state";
        const string initialValue = "v1";
        const string newValue = "v2";

        var handle = memberA.Gossip.RegisterConsensusCheck<SomeGossipState, string>(key, s => s.Key);

        try
        {
            foreach (var m in members)
            {
                await m.Gossip.SetStateAsync(key, new SomeGossipState { Key = initialValue });
            }

            await ClusterProbe.WaitForConsensusAsync(new[] { handle }, initialValue, TimeSpan.FromSeconds(10));

            GossipNetworkPartition.Isolate(memberB.System.Address);

            await memberA.Gossip.SetStateAsync(key, new SomeGossipState { Key = newValue });
            await ClusterProbe.WaitForNoConsensusAsync(new[] { handle }, TimeSpan.FromSeconds(5));

            var stateDuringPartition = await memberB.Gossip.GetState<SomeGossipState>(key);
            stateDuringPartition[memberA.System.Id].Key.Should().Be(initialValue);

            GossipNetworkPartition.Clear();

            // Re-emit the updated state so the previously partitioned member catches up
            await memberA.Gossip.SetStateAsync(key, new SomeGossipState { Key = newValue });
            await memberC.Gossip.SetStateAsync(key, new SomeGossipState { Key = newValue });

            await ClusterProbe.WaitForMemberStateAsync<SomeGossipState>(memberB, key, memberA.System.Id,
                s => s.Key == newValue, TimeSpan.FromSeconds(10));
            var stateAfterRecovery = await memberB.Gossip.GetState<SomeGossipState>(key);
            stateAfterRecovery[memberA.System.Id].Key.Should().Be(newValue);
        }
        finally
        {
            handle.Dispose();
        }
    }

    private class PartitionClusterFixture : BaseInMemoryClusterFixture
    {
        public PartitionClusterFixture() : base(3)
        {
        }

        protected override ActorSystemConfig GetActorSystemConfig()
        {
            var baseConfig = base.GetActorSystemConfig();
            return baseConfig
                .WithConfigureProps(p => baseConfig.ConfigureProps(p).WithSenderMiddleware(GossipNetworkPartition.Middleware))
                .WithConfigureSystemProps((name, p) => baseConfig.ConfigureSystemProps(name, p).WithSenderMiddleware(GossipNetworkPartition.Middleware));
        }
    }

    private static class GossipNetworkPartition
    {
        private static readonly HashSet<string> Dropped = new();

        public static void Isolate(string address) => Dropped.Add(address);
        public static void Clear() => Dropped.Clear();

        public static Func<Sender, Sender> Middleware => next => async (ctx, target, envelope) =>
        {
            if (envelope.Message is GossipRequest && target.Id == Gossiper.GossipActorName)
            {
                var from = ctx.System.Address;
                var to = target.Address;

                if (Dropped.Contains(from) || Dropped.Contains(to))
                {
                    return;
                }
            }

            await next(ctx, target, envelope);
        };
    }
}
