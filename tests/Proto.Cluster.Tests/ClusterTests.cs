namespace Proto.Cluster.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ClusterTest.Messages;
    using FluentAssertions;
    using Remote.Tests.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public class ClusterTests :ClusterTestBase, IClassFixture<InMemoryClusterFixture>
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ClusterTests(ITestOutputHelper testOutputHelper, InMemoryClusterFixture clusterFixture): base(clusterFixture)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(1000, 5000)]
        public async Task CanSpawnConcurrently(int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;


            await PingAllConcurrently(Members[0]);

            async Task PingAllConcurrently(Cluster cluster)
            {
                await Task.WhenAll(
                    GetActorIds(count).Select(async id =>
                        {
                            Pong pong = null;
                            while (pong == null)
                            {
                                timeout.ThrowIfCancellationRequested();
                                pong = await cluster.Ping(id, id, timeout);
                                _testOutputHelper.WriteLine($"{id} received response {pong?.Message}");
                            }

                            pong.Message.Should().Be($"{id}:{id}");
                        }
                    )
                );
            }
        }

        [Theory]
        [InlineData(1000, 5000)]
        public async Task CanSpawnSequentially(int count, int msTimeout)
        {
            var timeout = new CancellationTokenSource(msTimeout).Token;

            await PingAllSequentially(Members[0]);

            async Task PingAllSequentially(Cluster cluster)
            {
                foreach (var id in GetActorIds(count))
                {
                    Pong pong = null;
                    while (pong == null)
                    {
                        timeout.ThrowIfCancellationRequested();
                        pong = await cluster.Ping(id, id, timeout);
                    }

                    pong.Message.Should().Be($"{id}:{id}");
                }
            }
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

        [Fact]
        public async Task CanCollectHeartbeatMetrics()
        {
            var timeout = new CancellationTokenSource(5000);


            await PingAll("ping1", timeout.Token);
            var count = await GetActorCountFromHeartbeat();
            count.Should().BePositive();

            const int virtualActorCount = 10;
            foreach (var id in Enumerable.Range(1, virtualActorCount))
            {
                await PingAll(id.ToString(), timeout.Token);
            }

            var afterPing = await GetActorCountFromHeartbeat();

            afterPing.Should().Be(count + virtualActorCount, "We expect the echo actors to be added to the count");


            async Task<int> GetActorCountFromHeartbeat()
            {
                var heartbeatResponses = await Task.WhenAll(Members.Select(c =>
                        c.System.Root.RequestAsync<HeartbeatResponse>(
                            PID.FromAddress(c.System.Address, "ClusterHeartBeat"), new HeartbeatRequest(), timeout.Token
                        )
                    )
                );
                return heartbeatResponses.Select(response => (int) response.ActorCount).Sum();
            }

            async Task PingAll(string identity, CancellationToken token)
            {
                foreach (var cluster in Members)
                {
                    await cluster.Ping(identity, "", token);
                }
            }
        }

        private static async Task PingPong(Cluster cluster, string id, CancellationToken token = default)
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