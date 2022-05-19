﻿// -----------------------------------------------------------------------
// <copyright file="GossipTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Gossip;
using Proto.Logging;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

public class GossipTests
{
    private readonly ITestOutputHelper _testOutputHelper;
    //
    protected GossipTests(ITestOutputHelper testOutputHelper)
    {
        _testOutputHelper = testOutputHelper;
    }

    private const string GossipStateKey = "test-state";
    private const string TopologyStateKey = "topology-test-state";

    [Fact]
    public async Task CanGetConsensus()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync().ConfigureAwait(false);

        const string initialValue = "hello consensus";

        var fixtureMembers = clusterFixture.Members;
        var consensusChecks = fixtureMembers.Select(CreateConsensusCheck).ToList();

        SetGossipState(fixtureMembers, initialValue);

        await ShouldBeInConsensusAboutValue(consensusChecks, initialValue);
    }

    [Fact(Skip = "Flaky")]
    public async Task CompositeConsensusWorks()
    {
        var timeout = CancellationTokens.FromSeconds(20);
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync().ConfigureAwait(false);

        await Task.Delay(1000);
            
        var (consensus, initialTopologyHash) =
            await clusterFixture.Members.First().MemberList.TopologyConsensus(timeout);
        consensus.Should().BeTrue();

        var fixtureMembers = clusterFixture.Members;
        var consensusChecks = fixtureMembers.Select(CreateCompositeConsensusCheck).ToList();

        var firstNodeCheck = consensusChecks[0];
        var notConsensus = await firstNodeCheck.TryGetConsensus(TimeSpan.FromMilliseconds(200), timeout);

        notConsensus.consensus.Should().BeFalse("We have not set the correct topology hash in the state yet");

        await SetTopologyGossipStateAsync(fixtureMembers, initialTopologyHash);

        var afterSettingMatchingState = await firstNodeCheck.TryGetConsensus(TimeSpan.FromSeconds(20), timeout);

        afterSettingMatchingState.consensus.Should().BeTrue("After assigning the matching topology hash, there should be consensus");
        afterSettingMatchingState.value.Should().Be(initialTopologyHash);

        await clusterFixture.SpawnNode();
        await Task.Delay(2000); // Allow topology state to propagate

        var afterChangingTopology =
            await firstNodeCheck.TryGetConsensus(TimeSpan.FromMilliseconds(500), timeout);

        afterChangingTopology.consensus.Should().BeFalse("The state does no longer match the current topology");
    }

    [Fact]
    public async Task CanFallOutOfConsensus()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        const string initialValue = "hello consensus";
        const string otherValue = "hi";

        var consensusChecks = clusterFixture.Members.Select(CreateConsensusCheck).ToList();

        SetGossipState(clusterFixture.Members, initialValue);

        await ShouldBeInConsensusAboutValue(consensusChecks, initialValue);

        var firstMember = clusterFixture.Members[0];
        var firstMemberConsensus = consensusChecks[0];

        // var logStore = new LogStore();
        // firstMember.System.Extensions.Register(new InstanceLogger(LogLevel.Debug, logStore));

        // Sets a now inconsistent state on the first node
        await firstMember.Gossip.SetStateAsync(GossipStateKey, new SomeGossipState {Key = otherValue});

        var afterSettingDifferingState = await GetCurrentConsensus(firstMember, TimeSpan.FromMilliseconds(5000));

        afterSettingDifferingState.Should()
            .BeEquivalentTo((false, (string) null), "We should be able to read our writes, and locally we do not have consensus");

        await Task.Delay(5000);
        
        await clusterFixture.Members.DumpClusterState(_testOutputHelper);
        await ShouldBeNotHaveConsensus(consensusChecks);
    }

    private static async Task ShouldBeInConsensusAboutValue(List<IConsensusHandle<string>> consensusChecks, string initialValue)
    {
        var results = await Task.WhenAll(consensusChecks.Select(it => it.TryGetConsensus(CancellationTokens.FromSeconds(5))))
            .ConfigureAwait(false);

        foreach (var (consensus, consensusValue) in results)
        {
            consensus.Should().BeTrue("Since all nodes have the same value, they should agree on a consensus");

            consensusValue.Should().Be(initialValue);
        }
    }

    [Fact]
    private void EnumerableExtensionIsCorrect()
    {
        new[] {1, 2, 3}.HasConsensus().Item1.Should().BeFalse();
        new[] {1, 1, 1}.HasConsensus().Item1.Should().BeTrue();
        new int[] { }.HasConsensus().Item1.Should().BeFalse();
    }

    private static async Task ShouldBeNotHaveConsensus(List<IConsensusHandle<string>> consensusChecks)
    {
        var results = await Task.WhenAll(consensusChecks.Select(it => it.TryGetConsensus(CancellationTokens.FromSeconds(1))))
            .ConfigureAwait(false);

        foreach (var (consensus, _) in results)
        {
            consensus.Should().BeFalse("The cluster is not in consensus");
        }
    }

    private static void SetGossipState(IList<Cluster> members, string value)
    {
        foreach (var member in members)
        {
            member.Gossip.SetState(GossipStateKey, new SomeGossipState {Key = value});
        }
    }

    private static Task SetTopologyGossipStateAsync(IList<Cluster> members, ulong value) => Task.WhenAll(
        members.Select(member => member.Gossip.SetStateAsync(TopologyStateKey, new SomeTopologyGossipState {TopologyHash = value}))
    );

    private static IConsensusHandle<string> CreateConsensusCheck(Cluster member) => member.Gossip.RegisterConsensusCheck<SomeGossipState, string>(
        GossipStateKey, rebalance => rebalance.Key
    );

    private static IConsensusHandle<ulong> CreateCompositeConsensusCheck(Cluster member) =>
        member.Gossip.RegisterConsensusCheck<ulong>(Gossiper.ConsensusCheckBuilder<ulong>
            .Create<SomeTopologyGossipState>(TopologyStateKey, state => state.TopologyHash)
            .InConsensusWith<ClusterTopology>(GossipKeys.Topology, topology => topology.TopologyHash)
        );

    private static async Task<(bool consensus, string value)> GetCurrentConsensus(Cluster member, TimeSpan timeout)
    {
        using var check = CreateConsensusCheck(member);

        return await check.TryGetConsensus(timeout, CancellationToken.None).ConfigureAwait(false);
    }
}