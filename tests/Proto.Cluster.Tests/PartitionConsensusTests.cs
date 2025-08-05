using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.Utils;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class PartitionConsensusTests
{
    private const string GossipStateKey = "partition-state";

    [Fact]
    public async Task Consensus_fails_during_partition_and_recovers_after()
    {
        // Start an in-memory cluster with three members
        var fixture = new PartitionClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var members = fixture.Members;

        // Register a consensus check for each member and verify initial agreement
        var initialChecks = members.Select(CreateConsensusCheck).ToList();

        await SetGossipStateAsync(members, "v1");
        await ShouldBeInConsensus(initialChecks, "v1");

        foreach (var c in initialChecks)
        {
            c.Dispose();
        }

        var memberA = members[0];
        var memberB = members[1];
        var memberC = members[2];

        // Simulate a network partition by blocking memberB from receiving gossip from A and C.
        memberB.Remote.BlockList.Block(new[] { memberA.System.Id, memberC.System.Id }, "partition");

        // Update gossip state on all members to a new value. B cannot verify
        // the others' value due to the partition.
        await SetGossipStateAsync(members, "v2");

        // Create a fresh consensus check for the partitioned member
        var check = CreateConsensusCheck(memberB);

        // Attempt to reach consensus while the partition is active
        var result = await check.TryGetConsensus(TimeSpan.FromMilliseconds(200), CancellationTokens.FromSeconds(5));
        result.consensus.Should().BeFalse("partition should prevent consensus");
        check.Dispose();

        // Wait long enough for the partition to heal (BlockedMemberDuration)
        await Task.Delay(6000);

        // Trigger another gossip update so the healed cluster can converge on v2
        await memberA.Gossip.SetStateAsync(GossipStateKey, new SomeGossipState { Key = "v2" });

        // Create a new check after the partition heals and verify consensus
        using var healedCheck = CreateConsensusCheck(memberB);
        result = await healedCheck.TryGetConsensus(TimeSpan.FromSeconds(5), CancellationTokens.FromSeconds(10));
        result.consensus.Should().BeTrue();
        result.value.Should().Be("v2");
    }

    private static Task SetGossipStateAsync(IList<Cluster> members, string value) =>
        Task.WhenAll(members.Select(member =>
            member.Gossip.SetStateAsync(GossipStateKey, new SomeGossipState { Key = value }))
        );

    private static IConsensusHandle<string> CreateConsensusCheck(Cluster member) =>
        member.Gossip.RegisterConsensusCheck<SomeGossipState, string>(
            GossipStateKey, state => state.Key
        );

    // Helper that asserts all provided checks reach consensus on the expected value
    private static async Task ShouldBeInConsensus(List<IConsensusHandle<string>> checks, string value)
    {
        var results = await Task.WhenAll(
            checks.Select(c => c.TryGetConsensus(CancellationTokens.FromSeconds(5)))
        );

        foreach (var (consensus, v) in results)
        {
            consensus.Should().BeTrue();
            v.Should().Be(value);
        }
    }

    private class PartitionClusterFixture : InMemoryClusterFixture
    {
        protected override ActorSystemConfig GetActorSystemConfig() =>
            base.GetActorSystemConfig() with { BlockedMemberDuration = TimeSpan.FromMilliseconds(500) };
    }
}

