// -----------------------------------------------------------------------
// <copyright file="GossipTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Gossip;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossipTests
{
    private const string GossipStateKey = "test-state";
    private const string TopologyStateKey = "topology-test-state";

    private readonly ITestOutputHelper _testOutputHelper;

    //
    public GossipTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public async Task CanGetConsensus()
    {
        var clusterFixture = new InMemoryClusterFixture();
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        const string initialValue = "hello consensus";

        var fixtureMembers = clusterFixture.Members;
        var consensusChecks = fixtureMembers.Select(CreateConsensusCheck).ToList();

        SetGossipState(fixtureMembers, initialValue);

        _testOutputHelper.WriteLine(await clusterFixture.Members.DumpClusterState());
        await ShouldBeInConsensusAboutValue(consensusChecks, initialValue);

    }

    [Fact(Skip = "Flaky")]
    public async Task CompositeConsensusWorks()
    {
        var timeout = CancellationTokens.FromSeconds(20);
        var clusterFixture = new InMemoryClusterFixture();
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        var topologyResults = await Task.WhenAll(
            clusterFixture.Members.Select(m => m.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5))));
        var initialTopologyHash = topologyResults[0].topologyHash;

        var fixtureMembers = clusterFixture.Members;
        var consensusChecks = fixtureMembers.Select(CreateCompositeConsensusCheck).ToList();

        var firstNodeCheck = consensusChecks[0];
        var notConsensus = await firstNodeCheck.TryGetConsensus(TimeSpan.FromMilliseconds(200), timeout);

        notConsensus.consensus.Should().BeFalse("We have not set the correct topology hash in the state yet");

        await SetTopologyGossipStateAsync(fixtureMembers, initialTopologyHash);

        var afterSettingMatchingState = await firstNodeCheck.TryGetConsensus(TimeSpan.FromSeconds(20), timeout);

        afterSettingMatchingState.consensus.Should()
            .BeTrue("After assigning the matching topology hash, there should be consensus");

        afterSettingMatchingState.value.Should().Be(initialTopologyHash);

        await clusterFixture.SpawnMember();
        await Task.WhenAll(clusterFixture.Members
            .Select(m => m.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5))));

        var afterChangingTopology =
            await firstNodeCheck.TryGetConsensus(TimeSpan.FromMilliseconds(500), timeout);

        afterChangingTopology.consensus.Should().BeFalse("The state does no longer match the current topology");

    }

    [Fact]
    public async Task StateProbeReplicatesState()
    {
        var clusterFixture = new InMemoryClusterFixture();
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        await Task.WhenAll(clusterFixture.Members
            .Select(m => m.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5))));

        var memberA = clusterFixture.Members[0];
        var memberB = clusterFixture.Members[1];

        var probeHelper = new ClusterProbe(memberA);
        using var probe = await probeHelper.CreateStateProbeAsync();

        await ClusterProbe.WaitForMemberStateAsync<SomeGossipState>(memberB, probe.Key, memberA.System.Id,
            s => s.Key == probe.Value, TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task Gossip_should_replicate_large_state_with_small_batches()
    {
        const int memberCount = 5;
        const int fanout = 2;
        const int maxSend = 2;
        const int keysPerMember = 5;

        var clusterFixture = new GossipClusterFixture(memberCount, fanout, maxSend);
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        var expected = clusterFixture.Members.ToDictionary(
            m => m.System.Id,
            _ => new Dictionary<string, string>()
        );

        foreach (var (member, index) in clusterFixture.Members.Select((m, i) => (m, i)))
        {
            for (var i = 0; i < keysPerMember; i++)
            {
                var key = $"k{index}-{i}";
                var value = $"v{index}-{i}";
                await member.Gossip.SetStateAsync(key, new SomeGossipState { Key = value });
                expected[member.System.Id][key] = value;
            }
        }

        var ct = CancellationTokens.FromSeconds(20);
        while (!ct.IsCancellationRequested && !await AllReplicated())
        {
            await Task.Delay(50, ct);
        }

        (await AllReplicated()).Should().BeTrue();

        async Task<bool> AllReplicated()
        {
            var snap = await clusterFixture.Members[0].Gossip.GetStateSnapshot();
            if (snap.Members.Count != memberCount)
            {
                return false;
            }

            foreach (var (ownerId, kvs) in expected)
            {
                if (!snap.Members.TryGetValue(ownerId, out var ms))
                {
                    return false;
                }

                foreach (var (key, value) in kvs)
                {
                    if (!ms.Values.TryGetValue(key, out var any) ||
                        !any.Value.TryUnpack<SomeGossipState>(out var state) ||
                        state.Key != value)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }

    private static async Task ShouldBeInConsensusAboutValue(List<IConsensusHandle<string>> consensusChecks,
        string initialValue)
    {
        var results = await Task
            .WhenAll(consensusChecks.Select(it => it.TryGetConsensus(CancellationTokens.FromSeconds(5))))
            ;

        foreach (var (consensus, consensusValue) in results)
        {
            consensus.Should().BeTrue("Since all nodes have the same value, they should agree on a consensus");

            consensusValue.Should().Be(initialValue);
        }
    }

    [Fact]
    private void EnumerableExtensionIsCorrect()
    {
        new[] { 1, 2, 3 }.HasConsensus().Item1.Should().BeFalse();
        new[] { 1, 1, 1 }.HasConsensus().Item1.Should().BeTrue();
        new int[] { }.HasConsensus().Item1.Should().BeFalse();
    }

    private static void SetGossipState(IList<Cluster> members, string value)
    {
        foreach (var member in members)
        {
            member.Gossip.SetState(GossipStateKey, new SomeGossipState { Key = value });
        }
    }

    private static Task SetTopologyGossipStateAsync(IList<Cluster> members, ulong value) =>
        Task.WhenAll(
            members.Select(member =>
                member.Gossip.SetStateAsync(TopologyStateKey, new SomeTopologyGossipState { TopologyHash = value }))
        );

    private static IConsensusHandle<string> CreateConsensusCheck(Cluster member) =>
        member.Gossip.RegisterConsensusCheck<SomeGossipState, string>(
            GossipStateKey, rebalance => rebalance.Key
        );

    private static IConsensusHandle<ulong> CreateCompositeConsensusCheck(Cluster member) =>
        member.Gossip.RegisterConsensusCheck(Gossiper.ConsensusCheckBuilder<ulong>
            .Create<SomeTopologyGossipState>(TopologyStateKey, state => state.TopologyHash)
            .InConsensusWith<ClusterTopology>(GossipKeys.Topology, topology => topology.TopologyHash)
        );

    private static async Task<(bool consensus, string value)> GetCurrentConsensus(Cluster member, TimeSpan timeout)
    {
        using var check = CreateConsensusCheck(member);

        return await check.TryGetConsensus(timeout, CancellationToken.None);
    }

    private sealed class GossipClusterFixture : BaseInMemoryClusterFixture
    {
        public GossipClusterFixture(int clusterSize, int fanout, int maxSend)
            : base(clusterSize, config => config
                .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
                .WithGossipFanOut(fanout)
                .WithGossipMaxSend(maxSend))
        {
        }
    }
}
