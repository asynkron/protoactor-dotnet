namespace Proto.Cluster.Tests
{
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ClusterTest.Messages;
    using FluentAssertions;
    using Xunit;
    using Xunit.Abstractions;

    public abstract class ClusterTests : ClusterTestBase
    {
        private readonly ITestOutputHelper _testOutputHelper;

        protected ClusterTests(ITestOutputHelper testOutputHelper, IClusterFixture clusterFixture) : base(clusterFixture
        )
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public async Task ReSpawnsClusterActorsFromDifferentNodes()
        {
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
            await PingPong(otherNode, id, timeout);
            timer.Stop();

            _testOutputHelper.WriteLine("Respawned virtual actor in {0}", timer.Elapsed);
        }

        [Theory]
        [InlineData(1000, 10000)]
        public async Task CanSpawnVirtualActorsSequentially(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            foreach (var id in GetActorIds(actorCount))
            {
                await PingPong(entryNode, id, timeout);
            }

            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory]
        [InlineData(1000, 10000)]
        public async Task CanSpawnVirtualActorsConcurrently(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            await Task.WhenAll(GetActorIds(actorCount).Select(id => PingPong(entryNode, id, timeout)));
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory]
        [InlineData(100, 6000)]
        public async Task CanSpawnMultipleKindsWithSameIdentityConcurrently(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            var actorIds = GetActorIds(actorCount);
            await Task.WhenAll(actorIds.Select(id => Task.WhenAll(
                        PingPong(entryNode, id, timeout, EchoActor.Kind),
                        PingPong(entryNode, id, timeout, EchoActor.Kind2)
                    )
                )
            );
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount * 2} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory]
        [InlineData(100, 4000)]
        public async Task CanSpawnVirtualActorsConcurrentlyOnAllNodes(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var timer = Stopwatch.StartNew();
            await Task.WhenAll(Members.SelectMany(member =>
                    GetActorIds(actorCount).Select(id => PingPong(member, id, timeout))
                )
            );
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory]
        [InlineData(100, 4000)]
        public async Task CanRespawnVirtualActors(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();

            var ids = GetActorIds(actorCount).ToList();

            await Task.WhenAll(ids.Select(id => PingPong(entryNode, id, timeout)));
            await Task.WhenAll(ids.Select(id =>
                    entryNode.RequestAsync<Ack>(id, EchoActor.Kind, new Die(), timeout)
                )
            );
            await Task.WhenAll(ids.Select(id => PingPong(entryNode, id, timeout)));
            timer.Stop();
            _testOutputHelper.WriteLine(
                $"Spawned, killed and spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}"
            );
        }

        private async Task PingPong(Cluster cluster, string id, CancellationToken token = default,
            string kind = EchoActor.Kind)
        {
            await Task.Yield();
            
            var response = await cluster.Ping(id, id, new CancellationTokenSource(4000).Token, kind);
            var tries = 1;
            while (response == null && !token.IsCancellationRequested)
            {
                await Task.Delay(200,token);
                _testOutputHelper.WriteLine($"Retrying ping {kind}/{id}, attempt {++tries}");
                response = await cluster.Ping(id, id, new CancellationTokenSource(4000).Token, kind);
            }
            
            response.Should().NotBeNull($"We expect a response before timeout on {kind}/{id}");

            response.Should().BeEquivalentTo(new Pong
                {
                    Identity = id,
                    Kind = kind,
                    Message = id
                }, "Echo should come from the correct virtual actor"
            );
        }
    }

    // ReSharper disable once UnusedType.Global
    public class InMemoryClusterTests : ClusterTests, IClassFixture<InMemoryClusterFixture>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public InMemoryClusterTests(ITestOutputHelper testOutputHelper, InMemoryClusterFixture clusterFixture) : base(
            testOutputHelper, clusterFixture
        )
        {
        }
    }
}