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

        var leavingMember = fixture.Members.Last();
        var stayingMember = fixture.Members.First();

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
        await WaitUntil(() => fixture.Deliveries.Count == 1, "initial delivery");

        await fixture.RemoveNode(leavingMember, graceful: false);

        await fixture.PublishData(topic, 2);

        await WaitUntil(async () =>
        {
            var subs = await fixture.GetSubscribersForTopic(topic);
            return subs.Subscribers_.Count == 0;
        }, "Subscriber should be removed");

        fixture.Deliveries.Count.Should().Be(1);
    }

    private static async Task WaitUntil(Func<bool> condition, string? message = null, int timeoutMs = 5000, int delayMs = 100)
    {
        var stop = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < stop)
        {
            if (condition()) return;
            await Task.Delay(delayMs);
        }
        throw new TimeoutException(message ?? "Condition not met");
    }

    private static async Task WaitUntil(Func<Task<bool>> condition, string? message = null, int timeoutMs = 5000, int delayMs = 100)
    {
        var stop = DateTime.UtcNow + TimeSpan.FromMilliseconds(timeoutMs);
        while (DateTime.UtcNow < stop)
        {
            if (await condition()) return;
            await Task.Delay(delayMs);
        }
        throw new TimeoutException(message ?? "Condition not met");
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
            Members.First().Publisher().Publish(topic, new DataPublished(data), CancellationTokens.FromSeconds(5));
    }
}
