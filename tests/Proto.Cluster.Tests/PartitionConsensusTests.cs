using System;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.TestKit;
using Xunit;
using static Proto.TestKit.TestKit;

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

        var members = fixture.Members;
        var memberA = members[0];
        var memberB = members[1];

        // Wait until gossip knows about all members
        await AwaitConditionAsync(async () =>
        {
            var topology = await memberB.Gossip.GetState<ClusterTopology>(GossipKeys.Topology);
            return topology.Count == members.Count;
        }, TimeSpan.FromSeconds(10));

        const string key = "test-state";
        const string initialValue = "v1";
        const string newValue = "v2";

        foreach (var m in members)
        {
            await m.Gossip.SetStateAsync(key, new SomeGossipState { Key = initialValue });
        }

        // Wait for memberB to observe memberA's initial state
        await AwaitConditionAsync(async () =>
        {
            var state = await memberB.Gossip.GetState<SomeGossipState>(key);
            return state.TryGetValue(memberA.System.Id, out var s) && s.Key == initialValue;
        }, TimeSpan.FromSeconds(10));

        GossipNetworkPartition.Isolate(memberB.System.Address);

        await memberA.Gossip.SetStateAsync(key, new SomeGossipState { Key = newValue });

        // Ensure memberB still sees the old value while partitioned
        await AwaitConditionAsync(async () =>
        {
            var state = await memberB.Gossip.GetState<SomeGossipState>(key);
            return state.TryGetValue(memberA.System.Id, out var s) && s.Key == initialValue;
        }, TimeSpan.FromSeconds(5));
        var stateDuringPartition = await memberB.Gossip.GetState<SomeGossipState>(key);
        stateDuringPartition[memberA.System.Id].Key.Should().Be(initialValue);

        GossipNetworkPartition.Clear();

        // Wait for memberB to receive the updated value after partition clears
        await AwaitConditionAsync(async () =>
        {
            var state = await memberB.Gossip.GetState<SomeGossipState>(key);
            return state.TryGetValue(memberA.System.Id, out var s) && s.Key == newValue;
        }, TimeSpan.FromSeconds(10));
        var stateAfterRecovery = await memberB.Gossip.GetState<SomeGossipState>(key);
        stateAfterRecovery[memberA.System.Id].Key.Should().Be(newValue);
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
}
