// -----------------------------------------------------------------------
// <copyright file="GossipRequestValidationTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using FluentAssertions;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.Remote;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GossipRequestValidationTests
{
    [Fact]
    public async Task GossipRequest_from_unknown_member_should_be_rejected()
    {
        var clusterFixture = new InMemoryClusterFixture();
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        var target = clusterFixture.Members[0];
        var pid = PID.FromAddress(target.System.Address, Gossiper.GossipActorName);

        var response = await target.System.Root.RequestAsync<GossipResponse>(
            pid,
            new GossipRequest
            {
                MemberId = Guid.NewGuid().ToString("N"),
                State = new GossipState()
            },
            CancellationTokens.FromSeconds(5));

        response.Rejected.Should().BeTrue();
    }

    [Fact]
    public async Task GossipRequest_from_blocked_member_should_be_rejected()
    {
        var clusterFixture = new InMemoryClusterFixture();
        await using var _ = clusterFixture;
        await clusterFixture.InitializeAsync();

        var target = clusterFixture.Members[0];
        var blocked = clusterFixture.Members[1];

        var blockList = target.System.Remote().BlockList;
        blockList.Block(new[] { blocked.System.Id }, "test");

        var pid = PID.FromAddress(target.System.Address, Gossiper.GossipActorName);
        var response = await target.System.Root.RequestAsync<GossipResponse>(
            pid,
            new GossipRequest
            {
                MemberId = blocked.System.Id,
                State = new GossipState()
            },
            CancellationTokens.FromSeconds(5));

        response.Rejected.Should().BeTrue();
    }
}
