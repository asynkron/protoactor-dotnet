// -----------------------------------------------------------------------
// <copyright file = "PubSubTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.PubSub;
using Xunit;

namespace Proto.Cluster.Tests;

public class PubSubTests : IClassFixture<PubSubTests.PubSubInMemoryClusterFixture>
{
    private readonly PubSubInMemoryClusterFixture _fixture;

    public PubSubTests(PubSubInMemoryClusterFixture fixture)
    {
        _fixture = fixture;
        _fixture.Deliveries.Clear();
    }

    [Fact]
    public async Task Can_deliver_single_messages()
    {
        var subscriberIds = SubscriberIds("single-test", 20);
        const string topic = "single-test-topic";
        const int numMessages = 100;

        await SubscribeAllTo(topic, subscriberIds);

        for (var i = 0; i < numMessages; i++)
        {
            await PublishData(topic, i);
        }

        await UnsubscribeAllFrom(topic, subscriberIds);

        VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

    [Fact]
    public async Task Can_deliver_message_batches()
    {
        var subscriberIds = SubscriberIds("batch-test", 20);
        const string topic = "batch-test-topic";
        const int numMessages = 100;

        await SubscribeAllTo(topic, subscriberIds);

        for (var i = 0; i < numMessages / 10; i++)
        {
            var data = Enumerable.Range(i * 10, 10).ToArray();
            await PublishDataBatch(topic, data);
        }

        await UnsubscribeAllFrom(topic, subscriberIds);

        VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

    [Fact]
    public async Task Unsubscribed_actor_does_not_receive_messages()
    {
        const string sub1 = "unsubscribe-test-1";
        const string sub2 = "unsubscribe-test-2";
        const string topic = "unsubscribe-test";

        await SubscribeTo(topic, sub1);
        await SubscribeTo(topic, sub2);

        await UnsubscribeFrom(topic, sub2);

        await PublishData(topic, 1);

        _fixture.Deliveries.Should().HaveCount(1, "only one delivery should happen because the other actor is unsubscribed");
        _fixture.Deliveries.First().Identity.Should().Be(sub1, "the other actor should be unsubscribed");
    }

    [Fact]
    public async Task Can_subscribe_with_PID()
    {
        const string topic = "pid-subscribe";

        DataPublished deliveredMessage = null;

        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished d) deliveredMessage = d;
                return Task.CompletedTask;
            }
        );

        var member = _fixture.Members.First();
        var pid = member.System.Root.Spawn(props);
        await member.Subscribe(topic, pid);

        await PublishData(topic, 1);

        await member.Unsubscribe(topic, pid);

        deliveredMessage.Should().BeEquivalentTo(new DataPublished(1));
    }

    [Fact]
    public async Task Can_unsubscribe_with_PID()
    {
        const string topic = "pid-unsubscribe";

        var delivered = false;

        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished) delivered = true;
                return Task.CompletedTask;
            }
        );

        var member = _fixture.Members.First();
        var pid = member.System.Root.Spawn(props);

        await member.Subscribe(topic, pid);
        await member.Unsubscribe(topic, pid);

        await PublishData(topic, 1);

        delivered.Should().BeFalse();
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Can_publish_messages_via_batching_producer()
    {
        var subscriberIds = SubscriberIds("batching-producer-test", 20);
        const string topic = "batching-producer";
        const int numMessages = 100;

        await SubscribeAllTo(topic, subscriberIds);

        await using var producer = _fixture.Members.First().BatchingProducer(topic, new BatchingProducerConfig { BatchSize = 10 });

        var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
        await Task.WhenAll(tasks);

        await UnsubscribeAllFrom(topic, subscriberIds);

        VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

    private void VerifyAllSubscribersGotAllTheData(string[] subscriberIds, int numMessages)
    {
        var expected = subscriberIds
            .SelectMany(id => Enumerable.Range(0, numMessages).Select(i => new Delivery(id, i)))
            .ToArray();

        var actual = _fixture.Deliveries.OrderBy(d => d.Identity).ThenBy(d => d.Data).ToArray();

        actual.Should().Equal(expected, "the data published should be received by all subscribers");
    }

    private async Task SubscribeAllTo(string topic, string[] subscriberIds)
    {
        foreach (var id in subscriberIds)
        {
            await SubscribeTo(topic, id);
        }
    }

    private async Task UnsubscribeAllFrom(string topic, string[] subscriberIds)
    {
        foreach (var id in subscriberIds)
        {
            await UnsubscribeFrom(topic, id);
        }
    }

    private string[] SubscriberIds(string prefix, int count) => Enumerable.Range(1, count).Select(i => $"{prefix}-{i:D4}").ToArray();

    private Task SubscribeTo(string topic, string identity) => RequestViaRandomMember(identity, new Subscribe(topic));

    private Task UnsubscribeFrom(string topic, string identity) => RequestViaRandomMember(identity, new Unsubscribe(topic));

    private Task PublishData(string topic, int data) => PublishViaRandomMember(topic, new DataPublished(data));

    private Task PublishDataBatch(string topic, int[] data) => PublishViaRandomMember(topic, data.Select(d => new DataPublished(d)).ToArray());

    private readonly Random _random = new();

    private Task RequestViaRandomMember(string identity, object message) =>
        _fixture
            .Members[_random.Next(_fixture.Members.Count)]
            .RequestAsync<Response>(identity, PubSubInMemoryClusterFixture.SubscriberKind, message, CancellationTokens.FromSeconds(1));

    private Task PublishViaRandomMember(string topic, object message) =>
        _fixture
            .Members[_random.Next(_fixture.Members.Count)]
            .Publisher()
            .Publish(topic, message, CancellationTokens.FromSeconds(1));

    private Task PublishViaRandomMember<T>(string topic, T[] messages) =>
        _fixture
            .Members[_random.Next(_fixture.Members.Count)]
            .Publisher()
            .PublishBatch(topic, messages, CancellationTokens.FromSeconds(1));

    private record DataPublished(int Data);

    public record Delivery(string Identity, int Data);

    private record Subscribe(string Topic);

    private record Unsubscribe(string Topic);

    private record Response;

    public class PubSubInMemoryClusterFixture : BaseInMemoryClusterFixture
    {
        public const string SubscriberKind = "Subscriber";

        public ConcurrentBag<Delivery> Deliveries = new();

        public PubSubInMemoryClusterFixture() : base(1)
        {
        }

        protected override ClusterKind[] ClusterKinds => new[]
        {
            new ClusterKind(SubscriberKind, SubscriberProps())
        };

        private Props SubscriberProps()
        {
            async Task Receive(IContext context)
            {
                switch (context.Message)
                {
                    case DataPublished msg:
                        Deliveries.Add(new Delivery(context.ClusterIdentity()!.Identity, msg.Data));
                        context.Respond(new Response());
                        break;

                    case Subscribe msg:
                        await context.Cluster().Subscribe(msg.Topic, context.ClusterIdentity()!);
                        context.Respond(new Response());
                        break;

                    case Unsubscribe msg:
                        await context.Cluster().Unsubscribe(msg.Topic, context.ClusterIdentity()!);
                        context.Respond(new Response());
                        break;
                }
            }

            return Props.FromFunc(Receive);
        }
    }
}