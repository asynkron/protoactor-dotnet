using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.IdentityLookup;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    using System.Diagnostics;

    public class LocalAffinityStrategyTests : ClusterTestTemplate
    {
        public LocalAffinityStrategyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        public async Task PrefersLocalPlacement()
        {
            var clusters = await SpawnMembers(2);
            await Task.Delay(3000);
            TestOutputHelper.WriteLine("Cluster ready");
            var timeout = new CancellationTokenSource(100000).Token;

            var firstNode = clusters[0];

            await PingAll(firstNode);
            await PingAll(firstNode);

            var secondNode = clusters[1];
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

            secondNodeTimings.ElapsedMilliseconds.Should().BeLessThan(3000, "We expect dead letter responses instead of timeouts");

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

        protected override ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, string clusterName,
            IIdentityLookup identityLookup)
        {
            var config = base.GetClusterConfig(clusterProvider, clusterName, identityLookup)
                .WithClusterKind(EchoActor.Kind, EchoActor.Props.WithPoisonOnRemoteTraffic(.5f))
                .WithMemberStrategyBuilder((cluster, kind) => new LocalAffinityStrategy(cluster, 1100));
            config.RemoteConfig.WithProtoMessages(Remote.Tests.Messages.ProtosReflection.Descriptor);
            return config;
        }
    }
}