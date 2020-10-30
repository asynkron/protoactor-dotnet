namespace Proto.Cluster.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using global::ClusterTest.Messages;
    using FluentAssertions;
    using Remote.Tests.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public class RespawnTests :ClusterTest, IClassFixture<InMemoryClusterFixture>
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public RespawnTests(ITestOutputHelper testOutputHelper, InMemoryClusterFixture clusterFixture): base(clusterFixture)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task ReSpawnsClusterActorsFromDifferentNodesQuickly()
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            var timeout = new CancellationTokenSource(5000).Token;
            var id = CreateIdentity("1");
            await PingPong(Members[0], id, timeout);
            await PingPong(Members[1], id, timeout);

            //Retrieve the node the virtual actor was not spawned on
            var nodeLocation = await Members[0].RequestAsync<HereIAm>(id, EchoActor.Kind, new WhereAreYou(), timeout);
            nodeLocation.Should().NotBeNull("We expect the actor to respond correctly");
            var otherNode = Members.First(node => node.System.Address != nodeLocation.Address);

            //Kill it
            await otherNode.RequestAsync<Ack>(id, EchoActor.Kind, new Die(), timeout);

            var timer = Stopwatch.StartNew();
            // And force it to restart.
            // DeadLetterResponse should be sent to requestAsync, enabling a quick initialization of the new virtual actor
            await PingPong(otherNode, id);
            timer.Stop();

            timer.Elapsed.TotalMilliseconds.Should().BeLessThan(10,
                "We should not wait for timeouts for recreation of the virtual actor"
            );
        }


        [Theory]
        [InlineData(1000, 30000)]
        public async Task CanSpawnVirtualActors(int actorCount, int timeoutMs)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            await Task.WhenAll(Enumerable.Range(1, actorCount).Select(id => PingPong(entryNode, CreateIdentity(id.ToString()), timeout))
            );
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory]
        [InlineData(1000, 30000)]
        public async Task CanRespawnVirtualActors(int actorCount, int timeoutMs)
        {
            await Task.Delay(TimeSpan.FromSeconds(3));
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();

            var ids = GetActorIds(actorCount).ToList();

            await Task.WhenAll(ids.Select(id => PingPong(entryNode, id, timeout))
            );
            await Task.WhenAll(ids.Select(id =>
                    entryNode.RequestAsync<Ack>(id, EchoActor.Kind, new Die(), timeout)
                )
            );
            await Task.WhenAll(ids.Select(id => PingPong(entryNode, id, timeout))
            );
            timer.Stop();
            _testOutputHelper.WriteLine(
                $"Spawned, killed and spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}"
            );
        }


        private async Task PingPong(Cluster cluster, string id, CancellationToken token = default)
        {
            await Task.Yield();
            var response = await cluster.RequestAsync<Pong>(id, EchoActor.Kind, new Ping
                {
                    Message = id
                }, token
            );
            response.Should().NotBeNull("We expect a response before timeout");
            response.Message.Should().Be($"{id}:{id}", "Echo should come from the correct virtual actor");
        }
    }
}