using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.TestKit;
using Proto.Utils;
using Xunit;

namespace Proto.Cluster.Tests;

public class ExpectUpdatedTopologyConsensusTests
{
    [Fact]
    public async Task Completes_when_topology_changes()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        // Capture the initial topology hash
        (_, var initialHash) = await clusterFixture.Members.First()
            .MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));

        var updateTask = clusterFixture.Members.First().ExpectUpdatedTopologyConsensus();

        await clusterFixture.SpawnMember();

        var newHash = await updateTask;

        newHash.Should().NotBe(initialHash);
    }

    [Fact]
    public async Task Throws_when_topology_does_not_change()
    {
        await using var clusterFixture = new InMemoryClusterFixture();
        await clusterFixture.InitializeAsync();

        var updateTask = clusterFixture.Members.First()
            .ExpectUpdatedTopologyConsensus(TimeSpan.FromMilliseconds(200));

        await Assert.ThrowsAsync<TimeoutException>(() => updateTask);
    }
}
