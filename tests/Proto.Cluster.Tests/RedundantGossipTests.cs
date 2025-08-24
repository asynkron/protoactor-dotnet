// -----------------------------------------------------------------------
// <copyright file="RedundantGossipTests.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class RedundantGossipTests
{
    private const string GossipKey = "redundant-key";

    [Fact]
    public async Task GossipRequest_is_sent_multiple_times_without_state_changes()
    {
        // Arrange: create two in-memory members and set a gossip key once on the first member.
        // The key's sequence number will not change for the rest of the test.
        var fixture = new GossipCountingClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var sender = fixture.Members[0];
        sender.Gossip.SetState(GossipKey, new SomeGossipState { Key = "value" });

        var gossipPid = PID.FromAddress(sender.System.Address, Gossiper.GossipActorName);

        // Act: trigger multiple gossip rounds without mutating the gossip state.
        // MemberStateDelta is supposed to prevent resending unchanged keys by tracking
        // sequence numbers per member, yet the current gossip implementation ignores
        // these deltas and reserializes the full state every time.
        // If gossip respected sequence numbers, the key would be sent only once.
        const int rounds = 3;
        var tasks = Enumerable.Range(0, rounds)
            .Select(_ => sender.System.Root.RequestAsync<SendGossipStateResponse>(gossipPid, new SendGossipStateRequest()));

        await Task.WhenAll(tasks);

        // allow callbacks to process
        await Task.Delay(200);

        // Assert: the same key is serialized in every GossipRequest, despite no state changes.
        // This demonstrates redundant gossip traffic and shows that MemberStateDelta is not
        // respected.
        // TODO: Once MemberStateDelta is honored, update this test to expect a single
        // serialized key instead of one per round.
        fixture.SerializedKeyCount.Should().Be(rounds);
        fixture.SerializedKeyCount.Should().BeGreaterThan(1);
    }

    private class GossipCountingClusterFixture : BaseInMemoryClusterFixture
    {
        private int _serializedKeyCount;

        public int SerializedKeyCount => _serializedKeyCount;

        public GossipCountingClusterFixture() : base(2)
        {
        }

        protected override ActorSystemConfig GetActorSystemConfig() =>
            base.GetActorSystemConfig()
                // Intercept all system messages to count how many times the gossip key is serialized.
                .WithConfigureSystemProps((_, props) => props.WithReceiverMiddleware(GossipCountingReceiver));

        // Middleware that inspects outgoing GossipRequest messages and increments a counter
        // whenever the tracked key appears in the serialized state payload.
        private Receiver GossipCountingReceiver(Receiver next) => (ctx, envelope) =>
        {
            if (envelope.Message is GossipRequest request)
            {
                foreach (var memberState in request.State.Members.Values)
                {
                    if (memberState.Values.ContainsKey(GossipKey))
                    {
                        Interlocked.Increment(ref _serializedKeyCount);
                    }
                }
            }

            return next(ctx, envelope);
        };
    }
}
