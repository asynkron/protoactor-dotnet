namespace Proto.Cluster.Tests
{
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using FluentAssertions;
    using Remote.Tests.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public class LocalAffinityStrategyTests : ClusterTest,
        IClassFixture<LocalAffinityStrategyTests.LocalAffinityClusterFixture>
    {
        private ITestOutputHelper TestOutputHelper { get; }

        public LocalAffinityStrategyTests(ITestOutputHelper testOutputHelper,
            LocalAffinityClusterFixture clusterFixture) : base(clusterFixture)
        {
            TestOutputHelper = testOutputHelper;
        }


        [Fact]
        public async Task PrefersLocalPlacement()
        {
            await Task.Delay(3000);
            TestOutputHelper.WriteLine("Cluster ready");
            var timeout = new CancellationTokenSource(100000).Token;

            var firstNode = Members[0];

            await PingAll(firstNode);
            await PingAll(firstNode);

            var secondNode = Members[1];
            firstNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(1000,
                "We expect the actors to be localized to the node receiving traffic."
            );
            secondNode.System.ProcessRegistry.ProcessCount.Should().BeLessThan(100);

            TestOutputHelper.WriteLine(
                $"Actors: first node: {firstNode.System.ProcessRegistry.ProcessCount}, second node: {secondNode.System.ProcessRegistry.ProcessCount}"
            );
            var secondNodeTimings = Stopwatch.StartNew();
            await PingAll(secondNode);
            await PingAll(secondNode);
            await PingAll(secondNode);
            secondNodeTimings.Stop();


            TestOutputHelper.WriteLine("After traffic is shifted to second node:");
            TestOutputHelper.WriteLine(
                $"Actors: first node: {firstNode.System.ProcessRegistry.ProcessCount}, second node: {secondNode.System.ProcessRegistry.ProcessCount}"
            );

            firstNode.System.ProcessRegistry.ProcessCount.Should().BeInRange(100, 1000,
                "Some actors should have moved to the new node"
            );

            secondNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(100,
                "When traffic shifts to the second node, actors receiving remote traffic should start draining from the original node and be recreated"
            );

            secondNodeTimings.ElapsedMilliseconds.Should()
                .BeLessThan(3000, "We expect dead letter responses instead of timeouts");

            async Task PingAll(Cluster cluster)
            {
                await Task.WhenAll(
                    Enumerable.Range(0, 1000).Select(async i =>
                        {
                            Pong pong = null;
                            while (pong == null)
                            {
                                timeout.ThrowIfCancellationRequested();
                                pong = await cluster.Ping(i.ToString(), "hello", timeout);
                            }
                        }
                    )
                );
            }
        }

        public class LocalAffinityClusterFixture : BaseInMemoryClusterFixture
        {
            public LocalAffinityClusterFixture() : base(3,
                config =>
                {
                    config.WithMemberStrategyBuilder((cluster, kind) => new LocalAffinityStrategy(cluster, 1100));
                }
            )
            {
            }

            protected override (string, Props)[] ClusterKinds { get; } =
                {(EchoActor.Kind, EchoActor.Props.WithPoisonOnRemoteTraffic(.5f))};
        }
    }
}