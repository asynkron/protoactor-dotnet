using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Gossip;
using Proto.Cluster.Identity;
using Proto.Utils;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

public abstract class ClusterTests : ClusterTestBase
{
    protected readonly ITestOutputHelper _testOutputHelper;

    protected ClusterTests(ITestOutputHelper testOutputHelper, IClusterFixture clusterFixture)
        : base(clusterFixture)
    {
        _testOutputHelper = testOutputHelper;
    }

    [Fact]
    public void ClusterMembersMatch()
    {
        
        var memberSet = Members.First().MemberList.GetMembers();

        memberSet.Should().NotBeEmpty();

        Members.Skip(1).Select(member => member.MemberList.GetMembers()).Should().AllBeEquivalentTo(memberSet);
    }

    [Fact]
    public async Task CanSpawnASingleVirtualActor()
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token;

            var entryNode = Members[0];

            var timer = Stopwatch.StartNew();
            await PingPong(entryNode, "unicorn", timeout);
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned 1 actor in {timer.Elapsed}");
        }, _testOutputHelper);
    }
    
    [Fact]
    public async Task TopologiesShouldHaveConsensus()
    {
        await Trace(async () =>
        {
            var consensus = await Task
                .WhenAll(Members.Select(member =>
                    member.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(20))))
                .WaitUpTo(TimeSpan.FromSeconds(20))
                ;

            _testOutputHelper.WriteLine(await Members.DumpClusterState());

            consensus.completed.Should().BeTrue("All members should have gotten consensus on the same topology hash");
            _testOutputHelper.WriteLine(LogStore.ToFormattedString());
        }, _testOutputHelper);
    }

    [Fact]
    public async Task HandlesSlowResponsesCorrectly()
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(20000).Token;

            const string msg = "Hello-slow-world";

            var response = await Members.First()
                .RequestAsync<Pong>(CreateIdentity("slow-test"), EchoActor.Kind,
                    new SlowPing { Message = msg, DelayMs = 5000 }, timeout
                );

            response.Should().NotBeNull();
            response.Message.Should().Be(msg);
        }, _testOutputHelper);
    }

    [Fact]
    public async Task SupportsMessageEnvelopeResponses()
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(20000).Token;

            const string msg = "Hello-message-envelope";

            var response = await Members.First()
                .RequestAsync<MessageEnvelope>(CreateIdentity("message-envelope"),
                    EchoActor.Kind,
                    new Ping { Message = msg }, timeout
                );

            response.Should().NotBeNull();
            response.Should().BeOfType<MessageEnvelope>();
            response.Message.Should().BeOfType<Pong>();
        }, _testOutputHelper);
    }

    [Fact]
    public async Task StateIsReplicatedAcrossCluster()
    {
        await Trace(async () =>
        {
            if (ClusterFixture.ClusterSize < 2)
            {
                _testOutputHelper.WriteLine("Skipped test, cluster size is less than 2");

                return;
            }

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
        }, _testOutputHelper);
    }

    [Fact]
    public async Task ReSpawnsClusterActorsFromDifferentNodes()
    {
        await Trace(async () =>
        {
            if (ClusterFixture.ClusterSize < 2)
            {
                _testOutputHelper.WriteLine("Skipped test, cluster size is less than 2");

                return;
            }

            var timeout = new CancellationTokenSource(10000).Token;
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
        }, _testOutputHelper);
    }

    [Fact]
    public async Task HandlesLosingANode()
    {
        await Trace(async () =>
        {
            if (ClusterFixture.ClusterSize < 2)
            {
                _testOutputHelper.WriteLine("Skipped test, cluster size is less than 2");

                return;
            }

            var ids = Enumerable.Range(1, 10).Select(id => id.ToString()).ToList();

            await CanGetResponseFromAllIdsOnAllNodes(ids, Members, 20000);

            var toBeRemoved = Members.Last();
            _testOutputHelper.WriteLine("Removing node " + toBeRemoved.System.Id + " / " + toBeRemoved.System.Address);
            await ClusterFixture.RemoveNode(toBeRemoved);
            _testOutputHelper.WriteLine("Removed node " + toBeRemoved.System.Id + " / " + toBeRemoved.System.Address);
            await ClusterFixture.SpawnNode();

            await CanGetResponseFromAllIdsOnAllNodes(ids, Members, 20000);

            _testOutputHelper.WriteLine("All responses OK. Terminating fixture");
        }, _testOutputHelper);
    }

    [Fact]
    public async Task HandlesLosingANodeWhileProcessing()
    {
        await Trace(async () =>
        {
            if (ClusterFixture.ClusterSize < 2)
            {
                _testOutputHelper.WriteLine("Skipped test, cluster size is less than 2");

                return;
            }

            var ingressNodes = new[] { Members[0], Members[1] };
            var victim = Members[2];
            var ids = Enumerable.Range(1, 3).Select(id => id.ToString()).ToList();

            var cts = new CancellationTokenSource();

            var worker = Task.Run(async () =>
                {
                    while (!cts.IsCancellationRequested)
                    {
                        await CanGetResponseFromAllIdsOnAllNodes(ids, ingressNodes, 20000);
                    }
                }
            );

            await Task.Delay(1000);
            _testOutputHelper.WriteLine("Terminating node");
            await ClusterFixture.RemoveNode(victim);
            _testOutputHelper.WriteLine("Spawning node");
            await ClusterFixture.SpawnNode();
            await Task.Delay(1000);
            cts.Cancel();
            await worker;
        }, _testOutputHelper);
    }

    private async Task CanGetResponseFromAllIdsOnAllNodes(IEnumerable<string> actorIds, IList<Cluster> nodes,
        int timeoutMs)
    {
        var timer = Stopwatch.StartNew();
        var timeout = new CancellationTokenSource(timeoutMs).Token;
        await Task.WhenAll(nodes.SelectMany(entryNode => actorIds.Select(id => PingPong(entryNode, id, timeout))));
        _testOutputHelper.WriteLine("Got response from {0} nodes in {1}", nodes.Count(), timer.Elapsed);
    }

    [Theory]
    [InlineData(10, 10000)]
    public async Task CanSpawnVirtualActorsSequentially(int actorCount, int timeoutMs)
    {
        await Trace(async () =>
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
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10, 10000)]
    public async Task ConcurrentActivationsOnSameIdWorks(int clientCount, int timeoutMs)
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();
            var timer = Stopwatch.StartNew();

            var id = GetActorIds(clientCount).First();

            await Task.WhenAll(Enumerable.Range(0, clientCount).Select(_ => PingPong(entryNode, id, timeout)));

            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned 1 actor from {clientCount} clients in {timer.Elapsed}");
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10, 10000)]
    public async Task CanSpawnVirtualActorsConcurrently(int actorCount, int timeoutMs)
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            await Task.WhenAll(GetActorIds(actorCount).Select(id => PingPong(entryNode, id, timeout)));
            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10, 10000)]
    public async Task CanSpawnMultipleKindsWithSameIdentityConcurrently(int actorCount, int timeoutMs)
    {
        await Trace(async () =>
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
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10, 10000)]
    public async Task CanSpawnMultipleKindsWithSameIdentityConcurrentlyWhenUsingFilters(int actorCount, int timeoutMs)
    {
        await Trace(async () =>
        {
            using var cts = new CancellationTokenSource(timeoutMs);
            var timeout = cts.Token;

            var entryNode = Members.First();

            var timer = Stopwatch.StartNew();
            var actorIds = GetActorIds(actorCount);

            await Task.WhenAll(actorIds.Select(id => Task.WhenAll(
                        PingPong(entryNode, id, timeout, EchoActor.FilteredKind),
                        PingPong(entryNode, id, timeout, EchoActor.AsyncFilteredKind)
                    )
                )
            );

            timer.Stop();

            _testOutputHelper.WriteLine(
                $"Spawned {actorCount * 2} actors across {Members.Count} nodes in {timer.Elapsed}"
            );
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10, 10000, EchoActor.Kind)]
    [InlineData(10, 10000, EchoActor.FilteredKind)]
    [InlineData(10, 10000, EchoActor.AsyncFilteredKind)]
    public async Task CanSpawnVirtualActorsConcurrentlyOnAllNodes(int actorCount, int timeoutMs, string kind)
    {
        await Trace(async () =>
        {
            var timeout = new CancellationTokenSource(timeoutMs).Token;

            var timer = Stopwatch.StartNew();

            var tasks = Members.SelectMany(member =>
                GetActorIds(actorCount).Select(id => PingPong(member, id, timeout, kind))).ToList();

            await Task.WhenAll(tasks);

            timer.Stop();
            _testOutputHelper.WriteLine($"Spawned {actorCount} actors across {Members.Count} nodes in {timer.Elapsed}");
        }, _testOutputHelper);
    }

    [Theory]
    [InlineData(10000, EchoActor.FilteredKind)]
    [InlineData(10000, EchoActor.AsyncFilteredKind)]
    public async Task CanFilterActivations(int timeoutMs, string filteredKind) =>
        await Trace(async () =>
            {
                var timeout = new CancellationTokenSource(timeoutMs).Token;

                var member = Members.First();

                var invalidIdentity =
                    ClusterIdentity.Create(Tests.ClusterFixture.InvalidIdentity, filteredKind);

                var message = new Ping { Message = "Hello" };

                await member.Invoking(async m => await m.RequestAsync<Pong>(invalidIdentity, message, timeout))
                    .Should()
                    .ThrowExactlyAsync<IdentityIsBlockedException>();
            }, _testOutputHelper
        );

    [Theory]
    [InlineData(10, 20000)]
    public async Task CanRespawnVirtualActors(int actorCount, int timeoutMs)
    {
        await Trace(async () =>
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
        }, _testOutputHelper);
    }

    private async Task PingPong(
        Cluster cluster,
        string id,
        CancellationToken token = default,
        string kind = EchoActor.Kind,
        ISenderContext context= null
    )
    {
        await Task.Yield();

        Pong response = null;

        do
        {
            try
            {
                response = await cluster.Ping(id, id, CancellationTokens.FromSeconds(4), kind, context);
            }
            catch (TimeoutException)
            {
                // expected
            }

            if (response == null)
            {
                await Task.Delay(200, token);
            }
        } while (response == null && !token.IsCancellationRequested);

        response.Should().NotBeNull($"We expect a response before timeout on {kind}/{id}");

        response.Should()
            .BeEquivalentTo(new Pong
                {
                    Identity = id,
                    Kind = kind,
                    Message = id
                }, "Echo should come from the correct virtual actor"
            );
    }
}

// ReSharper disable once UnusedType.Global
public class InMemoryPartitionActivatorClusterTests : ClusterTests,
    IClassFixture<InMemoryClusterFixtureWithPartitionActivator>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public InMemoryPartitionActivatorClusterTests(ITestOutputHelper testOutputHelper,
        InMemoryClusterFixtureWithPartitionActivator clusterFixture)
        : base(testOutputHelper, clusterFixture)
    {
    }
}

public class SingleNodeProviderClusterTests : ClusterTests, IClassFixture<SingleNodeProviderFixture>
{
    // ReSharper disable once SuggestBaseTypeForParameterInConstructor
    public SingleNodeProviderClusterTests(ITestOutputHelper testOutputHelper, SingleNodeProviderFixture clusterFixture)
        : base(testOutputHelper, clusterFixture)
    {
    }
}