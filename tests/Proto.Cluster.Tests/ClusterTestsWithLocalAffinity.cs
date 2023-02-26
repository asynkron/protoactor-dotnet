using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

public abstract class ClusterTestsWithLocalAffinity : ClusterTests
{
    protected ClusterTestsWithLocalAffinity(ITestOutputHelper testOutputHelper, IClusterFixture clusterFixture)
        : base(testOutputHelper, clusterFixture)
    {
    }

    [Fact]
    public async Task LocalAffinityMovesActivationsOnRemoteSender()
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)).Token;
            var firstNode = Members[0];
            var secondNode = Members[1];

            await PingAndVerifyLocality(firstNode, timeout, "1:1", firstNode.System.Address,
                "Local affinity to sending node means that actors should spawn there"
            );

            LogProcessCounts();

            await PingAndVerifyLocality(secondNode, timeout, "2:1", firstNode.System.Address,
                "As the current instances exist on the 'wrong' node, these should respond before being moved"
            );

            LogProcessCounts();

            _testOutputHelper.WriteLine("Allowing time for actors to respawn..");
            await Task.Delay(200, timeout);
            LogProcessCounts();

            await PingAndVerifyLocality(secondNode, timeout, "2.2", secondNode.System.Address,
                "Relocation should be triggered, and the actors should be respawned on the local node"
            );

            LogProcessCounts();

            void LogProcessCounts() =>
                _testOutputHelper.WriteLine(
                    $"Processes: {firstNode.System.Address}: {firstNode.System.ProcessRegistry.ProcessCount}, {secondNode.System.Address}: {secondNode.System.ProcessRegistry.ProcessCount}"
                );
        }, _testOutputHelper);
    }

    private async Task PingAndVerifyLocality(
        Cluster cluster,
        CancellationToken token,
        string requestId,
        string expectResponseFrom = null,
        string because = null
    )
    {
        _testOutputHelper.WriteLine("Sending requests from " + cluster.System.Address);

        await Task.WhenAll(
            Enumerable.Range(0, 100)
                .Select(async i =>
                    {
                        var response = await cluster.RequestAsync<HereIAm>(CreateIdentity(i.ToString()),
                            EchoActor.LocalAffinityKind, new WhereAreYou
                            {
                                RequestId = requestId
                            }, token
                        );

                        response.Should().NotBeNull();

                        if (expectResponseFrom != null)
                        {
                            response.Address.Should().Be(expectResponseFrom, because);
                        }
                    }
                )
        );
    }
}

// ReSharper disable once UnusedType.Global
public class InMemoryClusterTests : ClusterTestsWithLocalAffinity, IClassFixture<InMemoryClusterFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameter
    public InMemoryClusterTests(ITestOutputHelper testOutputHelper, InMemoryClusterFixture clusterFixture) : base(
        testOutputHelper, clusterFixture
    )
    {
    }
}

// ReSharper disable once UnusedType.Global
public class InMemoryClusterTestsSharedFutures : ClusterTestsWithLocalAffinity,
    IClassFixture<InMemoryClusterFixtureSharedFutures>
{
    // ReSharper disable once SuggestBaseTypeForParameter
    public InMemoryClusterTestsSharedFutures(ITestOutputHelper testOutputHelper,
        InMemoryClusterFixtureSharedFutures clusterFixture) : base(
        testOutputHelper, clusterFixture
    )
    {
    }
}

// ReSharper disable once UnusedType.Global
public class InMemoryClusterTestsPidCacheInvalidation : ClusterTestsWithLocalAffinity,
    IClassFixture<InMemoryPidCacheInvalidationClusterFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameter
    public InMemoryClusterTestsPidCacheInvalidation(
        ITestOutputHelper testOutputHelper,
        InMemoryPidCacheInvalidationClusterFixture clusterFixture
    ) : base(
        testOutputHelper, clusterFixture
    )
    {
    }
}