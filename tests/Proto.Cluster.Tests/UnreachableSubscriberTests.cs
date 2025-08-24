using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Proto;
using Proto.Cluster.PubSub;
using Proto.Utils;
using Xunit;
using static Proto.TestKit.TestKit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class UnreachableSubscriberTests
{
    [Fact]
    public async Task Unreachable_subscribers_are_removed_after_dropped_message()
    {
        const string topic = "unreachable-drop";

        var fixture = new Fixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var leavingMember = fixture.Members[^1];
        var stayingMember = fixture.Members[0];

        var props = Props.FromFunc(ctx =>
        {
            if (ctx.Message is DataPublished msg)
            {
                fixture.Deliveries.Add(new Delivery(ctx.Self.ToDiagnosticString(), msg.Data));
                ctx.Respond(new Response());
            }

            return Task.CompletedTask;
        });

        var subscriberPid = leavingMember.System.Root.Spawn(props);
        await stayingMember.Subscribe(topic, subscriberPid);

        var subscribers = await fixture.GetSubscribersForTopic(topic);
        subscribers.Subscribers_.Should().Contain(s => s.Pid.Equals(subscriberPid));

        await fixture.PublishData(topic, 1);
        await AwaitConditionAsync(() => fixture.Deliveries.Count == 1, TimeSpan.FromSeconds(5),
            "initial delivery");

        await fixture.RemoveNode(leavingMember, graceful: false);

        await fixture.PublishData(topic, 2);

        await AwaitConditionAsync(async () =>
        {
            var subs = await fixture.GetSubscribersForTopic(topic);
            return subs.Subscribers_.Count == 0;
        }, TimeSpan.FromSeconds(5), "Subscriber should be removed");

        fixture.Deliveries.Count.Should().Be(1);
    }

    private record DataPublished(int Data);
    private record Delivery(string Identity, int Data);
    private record Response;

    private class Fixture : BaseInMemoryClusterFixture
    {
        public readonly ConcurrentBag<Delivery> Deliveries = new();
        private readonly InMemorySubscribersStore _store = new();

        public Fixture() : base(2, config => config
            .WithActorRequestTimeout(TimeSpan.FromSeconds(1))
            .WithPubSubConfig(PubSubConfig.Setup().WithSubscriberTimeout(TimeSpan.FromSeconds(1))))
        {
        }

        protected override ClusterKind[] ClusterKinds =>
            base.ClusterKinds
                .Concat(new[] { new ClusterKind(TopicActor.Kind, Props.FromProducer(() => new TopicActor(_store))) })
                .ToArray();

        public Task<Subscribers> GetSubscribersForTopic(string topic) => _store.GetAsync(topic, CancellationToken.None);

        public Task<PublishResponse> PublishData(string topic, int data) =>
            Members[0].Publisher().Publish(topic, new DataPublished(data), CancellationTokens.FromSeconds(5));
    }
}
