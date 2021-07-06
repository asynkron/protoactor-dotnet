using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public abstract class ClusterTests : ClusterTestBase
    {
        private readonly ITestOutputHelper _testOutputHelper;

        protected ClusterTests(ITestOutputHelper testOutputHelper, IClusterFixture clusterFixture)
            : base(clusterFixture) => _testOutputHelper = testOutputHelper;

        [Fact]
        public void ClusterMembersMatch()
        {
            var memberSet = Members.First().MemberList.GetMembers();

            memberSet.Should().NotBeEmpty();

            Members.Skip(1).Select(member => member.MemberList.GetMembers()).Should().AllBeEquivalentTo(memberSet);
        }

        [Fact]
        public async Task TopologiesShouldHaveConsensus()
        {
            var timeout = Task.Delay(20000);
        
            var consensus = Task.WhenAll(Members.Select(member => member.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(20))));
        
            await Task.WhenAny(timeout, consensus);

            _testOutputHelper.WriteLine(LogStore.ToFormattedString());
            timeout.IsCompleted.Should().BeFalse();
        }

        [Fact]
        public async Task HandlesSlowResponsesCorrectly()
        {
            var timeout = new CancellationTokenSource(8000).Token;

            var msg = "Hello-slow-world";
            var response = await Members.First().RequestAsync<Pong>(CreateIdentity("slow-test"), EchoActor.Kind,
                new SlowPing {Message = msg, DelayMs = 5000}, timeout
            );
            response.Should().NotBeNull();
            response.Message.Should().Be(msg);
        }

        [Fact]
        public async Task StateIsReplicatedAcrossCluster()
        {
            var sourceMember = Members.First();
            var sourceMemberId = sourceMember.System.Id;
            var targetMember = Members.Last();
            var targetMemberId = targetMember.System.Id;

            //make sure we somehow don't already have the expected value in the state of targetMember
            var initialResponse = await targetMember.Gossip.GetState<PID>("some-state");
            initialResponse.TryGetValue(sourceMemberId, out _).Should().BeFalse();

            //make sure we are not comparing the same not to itself;
            targetMemberId.Should().NotBe(sourceMemberId);

            var stream = SubscribeToGossipUpdates(targetMember);

            sourceMember.Gossip.SetState("some-state", new PID("abc", "def"));
            //allow state to replicate            
            await stream.FirstAsync(x => x.MemberId == sourceMemberId && x.Key == "some-state");

            //get state from target member
            //it should be noted that the response is a dict of member id for all members,
            //to the state for the given key for each of those members
            var response = await targetMember.Gossip.GetState<PID>("some-state");

            //get the state for source member
            response.TryGetValue(sourceMemberId, out var value).Should().BeTrue();

            value!.Address.Should().Be("abc");
            value.Id.Should().Be("def");

            IAsyncEnumerable<GossipUpdate> SubscribeToGossipUpdates(Cluster member)
            {
                var channel = Channel.CreateUnbounded<object>();
                member.System.EventStream.Subscribe(channel);
                var stream = channel.Reader.ReadAllAsync().OfType<GossipUpdate>();
                return stream;
            }
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

        [Fact]
        public async Task HandlesLosingANode()
        {
            var ids = Enumerable.Range(1, 200).Select(id => id.ToString()).ToList();

            await CanGetResponseFromAllIdsOnAllNodes(ids, Members, 5000);

            var toBeRemoved = Members.Last();
            _testOutputHelper.WriteLine("Removing node " + toBeRemoved.System.Id + " / " + toBeRemoved.System.Address);
            await ClusterFixture.RemoveNode(toBeRemoved);
            _testOutputHelper.WriteLine("Removed node " + toBeRemoved.System.Id + " / " + toBeRemoved.System.Address);
            await ClusterFixture.SpawnNode();

            await CanGetResponseFromAllIdsOnAllNodes(ids, Members, 5000);

            _testOutputHelper.WriteLine("All responses OK. Terminating fixture");
        }

        // [Fact]
        // public async Task HandlesLosingANodeWhileProcessing()
        // {
        //     var ingressNodes = new[] {Members[0], Members[1]};
        //     var victim = Members[2];
        //     var ids = Enumerable.Range(1, 200).Select(id => id.ToString()).ToList();
        //
        //     var cts = new CancellationTokenSource();
        //
        //     var worker = Task.Run(async () => {
        //             while (!cts.IsCancellationRequested)
        //             {
        //                 await CanGetResponseFromAllIdsOnAllNodes(ids, ingressNodes, 10000);
        //             }
        //         }
        //     );
        //     await Task.Delay(200);
        //     await ClusterFixture.RemoveNode(victim);
        //     await ClusterFixture.SpawnNode();
        //     await Task.Delay(1000);
        //     cts.Cancel();
        //     await worker;
        // }

        private async Task CanGetResponseFromAllIdsOnAllNodes(IEnumerable<string> actorIds, IList<Cluster> nodes, int timeoutMs)
        {
            var timer = Stopwatch.StartNew();
            var timeout = new CancellationTokenSource(timeoutMs).Token;
            await Task.WhenAll(nodes.SelectMany(entryNode => actorIds.Select(id => PingPong(entryNode, id, timeout))));
            _testOutputHelper.WriteLine("Got response from {0} nodes in {1}", nodes.Count(), timer.Elapsed);
        }

        [Theory, InlineData(100, 10000)]
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

        [Theory, InlineData(100, 10000)]
        public async Task ConcurrentActivationsOnSameIdWorks(int clientCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();
            var timer = Stopwatch.StartNew();

            var id = GetActorIds(clientCount).First();

            await Task.WhenAll(Enumerable.Range(0, clientCount).Select(_ => PingPong(entryNode, id, timeout)));

            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned 1 actor from {clientCount} clients in {timer.Elapsed}");
        }

        [Theory, InlineData(100, 10000)]
        public async Task CanSpawnVirtualActorsConcurrently(int actorCount, int timeoutMs)
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            await Task.WhenAll(GetActorIds(actorCount).Select(id => PingPong(entryNode, id, timeout)));
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }

        [Theory, InlineData(100, 10000)]
        public async Task CanSpawnMultipleKindsWithSameIdentityConcurrently(int actorCount, int timeoutMs)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var timeout = cts.Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            var actorIds = GetActorIds(actorCount);
            await Task.WhenAll(actorIds.Select(id => Task.WhenAll(
                        PingPong(entryNode, id, timeout),
                        PingPong(entryNode, id, timeout, EchoActor.Kind2)
                    )
                )
            );
            timer.Stop();
            _testOutputHelper.WriteLine(
                $"Spawned {actorCount * 2} actors across {Members.Count} nodes in {timer.Elapsed}"
            );
        }

        [Theory, InlineData(100, 10000)]
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

        [Theory, InlineData(100, 20000)]
        public async Task CanRespawnVirtualActors(int actorCount, int timeoutMs)
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var timeout = cts.Token;

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

        [Fact]
        public async Task LocalAffinityMovesActivationsOnRemoteSender()
        {
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;
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
            await Task.Delay(100, timeout);
            LogProcessCounts();

            await PingAndVerifyLocality(secondNode, timeout, "2.2", secondNode.System.Address,
                "Relocation should be triggered, and the actors should be respawned on the local node"
            );
            LogProcessCounts();

            void LogProcessCounts() => _testOutputHelper.WriteLine(
                $"Processes: {firstNode.System.Address}: {firstNode.System.ProcessRegistry.ProcessCount}, {secondNode.System.Address}: {secondNode.System.ProcessRegistry.ProcessCount}"
            );
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
                Enumerable.Range(0, 1000).Select(async i => {
                        var response = await cluster.RequestAsync<HereIAm>(CreateIdentity(i.ToString()), EchoActor.LocalAffinityKind, new WhereAreYou
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

        private async Task PingPong(
            Cluster cluster,
            string id,
            CancellationToken token = default,
            string kind = EchoActor.Kind
        )
        {
            await Task.Yield();

            var response = await cluster.Ping(id, id, new CancellationTokenSource(4000).Token, kind);
            var tries = 1;

            while (response == null && !token.IsCancellationRequested)
            {
                await Task.Delay(200, token);
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

    // ReSharper disable once UnusedType.Global
    public class InMemoryClusterTestsAlternativeClusterContext : ClusterTests, IClassFixture<InMemoryClusterFixtureAlternativeClusterContext>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public InMemoryClusterTestsAlternativeClusterContext(
            ITestOutputHelper testOutputHelper,
            InMemoryClusterFixtureAlternativeClusterContext clusterFixture
        ) : base(
            testOutputHelper, clusterFixture
        )
        {
        }
    }

    // ReSharper disable once UnusedType.Global
    public class InMemoryClusterTestsSharedFutures : ClusterTests, IClassFixture<InMemoryClusterFixtureSharedFutures>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public InMemoryClusterTestsSharedFutures(ITestOutputHelper testOutputHelper, InMemoryClusterFixtureSharedFutures clusterFixture) : base(
            testOutputHelper, clusterFixture
        )
        {
        }
    }

    // ReSharper disable once UnusedType.Global
    public class InMemoryClusterTestsPidCacheInvalidation : ClusterTests, IClassFixture<InMemoryPidCacheInvalidationClusterFixture>
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
}