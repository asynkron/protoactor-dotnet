namespace Proto.Cluster.Tests
{
    using System;
    using System.Diagnostics;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using ClusterTest.Messages;
    using Divergic.Logging.Xunit;
    using FluentAssertions;
    using Remote.Tests.Messages;
    using Xunit;
    using Xunit.Abstractions;

    public class RespawnTests : InMemDefaultClusterTests
    {
        public RespawnTests(ITestOutputHelper testOutputHelper)
        {
            var factory = LogFactory.Create(testOutputHelper);
            Log.SetLoggerFactory(factory);
        }

        [Fact]
        public async Task ReSpawnsDeadVirtualActorWithoutWaitingForTimeout()
        {
            var nodes = await SpawnClusterNodes(2);
            await Task.Delay(TimeSpan.FromSeconds(3));
            var timeout = new CancellationTokenSource(5000).Token;
            var id = "1";
            await PingPong(nodes[0], id, timeout);
            await PingPong(nodes[1], id, timeout);
            
            //Retrieve the node the virtual actor was not spawned on
            var nodeLocation = await nodes[0].RequestAsync<HereIAm>(id, EchoActor.Kind, new WhereAreYou(), timeout);
            nodeLocation.Should().NotBeNull("We expect the actor to respond correctly");
            var otherNode = nodes.Single(node => node.System.Address != nodeLocation.Address);
            
            //Kill it
            await otherNode.RequestAsync<Ack>(id, EchoActor.Kind, new Die(), timeout);
            
            var timer = Stopwatch.StartNew();
            // And force it to restart.
            // DeadLetterResponse should be sent to requestAsync, enabling a quick initialization of the new virtual actor
            await PingPong(otherNode, id);
            timer.Stop();

            timer.Elapsed.TotalMilliseconds.Should().BeLessThan(20,
                "We should not wait for timeouts for recreation of the virtual actor"
            );
        }
        
        
        [Theory]
        // [InlineData(1, 1000, 5000)]
        [InlineData(2, 1000, 30000)]

        public async Task CanRespawnVirtualActors(int clusterNodes, int actorCount, int timeoutMs)
        {
            var nodes = await SpawnClusterNodes(clusterNodes);
            await Task.Delay(TimeSpan.FromSeconds(3));
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = nodes.First();

            var timer = Stopwatch.StartNew();

            await Task.WhenAll(Enumerable.Range(1, actorCount).Select(id => PingPong(entryNode, id.ToString(), timeout)));
            await Task.WhenAll(Enumerable.Range(1, actorCount).Select(id =>
                entryNode.RequestAsync<Ack>(id.ToString(),EchoActor.Kind,new Die(),timeout)));
            await Task.WhenAll(Enumerable.Range(1, actorCount).Select(id => PingPong(entryNode, id.ToString(), timeout)));
            timer.Stop();

            // timer.Elapsed.TotalMilliseconds.Should().BeLessThan(3000,
            //     "Actors should be removed and recreated quickly");

        }
        

        private static async Task PingPong(Cluster cluster, string id, CancellationToken token = default)
        {
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