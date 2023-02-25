// -----------------------------------------------------------------------
// <copyright file = "PubSubTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// ----------------------------------------------------------------------- 

using System.Diagnostics.CodeAnalysis;
using FluentAssertions;
using Proto.Cluster.Tests;
using Xunit;
using Xunit.Abstractions;
using static Proto.Cluster.PubSub.Tests.WaitHelper;

namespace Proto.Cluster.PubSub.Tests;

[Collection("PubSub")] // The CI is just to slow to run cluster fixture based tests in parallel
public class PubSubTests : IClassFixture<PubSubClusterFixture>
{
    private readonly PubSubClusterFixture _fixture;
    private readonly ITestOutputHelper _output;

    public PubSubTests(PubSubClusterFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _fixture.Output = output;
        _output = output;
        _fixture.Deliveries.Clear();
    }

    [Fact]
    public async Task Can_deliver_single_messages()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("single-test", 20);
            const string topic = "single-test-topic";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds).ConfigureAwait(false);

            for (var i = 0; i < numMessages; i++)
            {
                var response = await _fixture.PublishData(topic, i).ConfigureAwait(false);

                if (response == null)
                {
                    _output.WriteLine(await _fixture.Members.DumpClusterState().ConfigureAwait(false));
                }

                response.Should().NotBeNull("publishing should not time out");
                response!.Status.Should().Be(PublishStatus.Ok);
            }

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Can_deliver_message_batches()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("batch-test", 20);
            const string topic = "batch-test-topic";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds).ConfigureAwait(false);

            for (var i = 0; i < numMessages / 10; i++)
            {
                var data = Enumerable.Range(i * 10, 10).ToArray();
                var response = await _fixture.PublishDataBatch(topic, data).ConfigureAwait(false);

                if (response == null)
                {
                    _output.WriteLine(await _fixture.Members.DumpClusterState().ConfigureAwait(false));
                }

                response.Should().NotBeNull("publishing should not time out");
            }

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Unsubscribed_actor_does_not_receive_messages()
    {
        await _fixture.Trace(async () =>
        {
            const string sub1 = "unsubscribe-test-1";
            const string sub2 = "unsubscribe-test-2";
            const string topic = "unsubscribe-test";

            await _fixture.SubscribeTo(topic, sub1).ConfigureAwait(false);
            await _fixture.SubscribeTo(topic, sub2).ConfigureAwait(false);

            await _fixture.UnsubscribeFrom(topic, sub2).ConfigureAwait(false);

            await _fixture.PublishData(topic, 1).ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false); // give time for the message "not to be delivered" to second subscriber

            await WaitUntil(() => _fixture.Deliveries.Count == 1,
                "only one delivery should happen because the other actor is unsubscribed").ConfigureAwait(false);

            _fixture.Deliveries.Should()
                .HaveCount(1, "only one delivery should happen because the other actor is unsubscribed");

            _fixture.Deliveries.First().Identity.Should().Be(sub1, "the other actor should be unsubscribed");
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Can_subscribe_with_PID()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "pid-subscribe";

            DataPublished? deliveredMessage = null;

            var props = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is DataPublished d)
                    {
                        deliveredMessage = d;
                    }

                    return Task.CompletedTask;
                }
            );

            var member = _fixture.Members.First();
            var pid = member.System.Root.Spawn(props);
            await member.Subscribe(topic, pid).ConfigureAwait(false);

            await _fixture.PublishData(topic, 1).ConfigureAwait(false);

            await WaitUntil(() => deliveredMessage != null, "Message should be delivered").ConfigureAwait(false);
            deliveredMessage.Should().BeEquivalentTo(new DataPublished(1));
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Can_unsubscribe_with_PID()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "pid-unsubscribe";

            var deliveryCount = 0;

            var props = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is DataPublished)
                    {
                        Interlocked.Increment(ref deliveryCount);
                    }

                    return Task.CompletedTask;
                }
            );

            var member = _fixture.Members.First();
            var pid = member.System.Root.Spawn(props);

            await member.Subscribe(topic, pid).ConfigureAwait(false);
            await member.Unsubscribe(topic, pid).ConfigureAwait(false);

            await _fixture.PublishData(topic, 1).ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false); // give time for the message "not to be delivered" to second subscriber

            deliveryCount.Should().Be(0);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Stopped_actor_that_did_not_unsubscribe_does_not_block_publishing_to_topic()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "missing-unsubscribe";

            var deliveryCount = 0;

            // this scenario is only relevant for regular actors,
            // virtual actors always exist, so the msgs should never be deadlettered 
            var props = Props.FromFunc(ctx =>
                {
                    if (ctx.Message is DataPublished)
                    {
                        Interlocked.Increment(ref deliveryCount);
                    }

                    return Task.CompletedTask;
                }
            );

            // spawn two actors and subscribe them to the topic
            var member = _fixture.Members.First();
            var pid1 = member.System.Root.Spawn(props);
            var pid2 = member.System.Root.Spawn(props);

            await member.Subscribe(topic, pid1).ConfigureAwait(false);
            await member.Subscribe(topic, pid2).ConfigureAwait(false);

            // publish one message
            await _fixture.PublishData(topic, 1).ConfigureAwait(false);
            await WaitUntil(() => deliveryCount == 2, "both messages should be delivered").ConfigureAwait(false);

            // kill one of the actors
            await member.System.Root.StopAsync(pid2).ConfigureAwait(false);

            // publish again
            var response = await _fixture.PublishData(topic, 2).ConfigureAwait(false);

            response.Should().NotBeNull("the publish operation shouldn't have timed out");
            await WaitUntil(() => deliveryCount == 3, "second publish should be delivered only to one of the actors").ConfigureAwait(false);

            await WaitUntil(async () =>
                {
                    var subscribers = await _fixture.GetSubscribersForTopic(topic).ConfigureAwait(false);

                    return !subscribers.Subscribers_!.Contains(new SubscriberIdentity { Pid = pid2 });
                }
            ).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Slow_PID_subscriber_that_times_out_does_not_prevent_subsequent_publishes()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "slow-pid-subscriber";

            var deliveryCount = 0;

            // a slow subscriber that times out
            var props = Props.FromFunc(async ctx =>
                {
                    await Task.Delay(4000,
                        _fixture
                            .CancelWhenDisposing).ConfigureAwait(false); // 4 seconds is longer than the subscriber timeout configured in the test fixture

                    Interlocked.Increment(ref deliveryCount);
                }
            );

            // subscribe
            var member = _fixture.RandomMember();
            var pid = member.System.Root.Spawn(props);
            await member.Subscribe(topic, pid).ConfigureAwait(false);

            // publish one message
            await _fixture.PublishData(topic, 1).ConfigureAwait(false);

            // next published message should also be delivered
            await _fixture.PublishData(topic, 1).ConfigureAwait(false);

            await WaitUntil(() => deliveryCount == 2,
                "A timing out subscriber should not prevent subsequent publishes", TimeSpan.FromSeconds(10)
            ).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Slow_ClusterIdentity_subscriber_that_times_out_does_not_prevent_subsequent_publishes()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "slow-ci-subscriber";

            // subscribe
            await _fixture.SubscribeTo(topic, "slow-ci-1", PubSubClusterFixture.TimeoutSubscriberKind).ConfigureAwait(false);

            // publish one message
            await _fixture.PublishData(topic, 1).ConfigureAwait(false);

            // next published message should also be delivered
            await _fixture.PublishData(topic, 1).ConfigureAwait(false);

            await WaitUntil(() => _fixture.Deliveries.Count == 2,
                "A timing out subscriber should not prevent subsequent publishes", TimeSpan.FromSeconds(10)
            ).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact(Skip = "Flaky")]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Can_publish_messages_via_batching_producer()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("batching-producer-test", 20);
            const string topic = "batching-producer";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds).ConfigureAwait(false);

            var producer = _fixture.Members.First()
                .BatchingProducer(topic, new BatchingProducerConfig { BatchSize = 10 });
            await using var _ = producer.ConfigureAwait(false);

            var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages).ConfigureAwait(false);
        }).ConfigureAwait(false);
    }

    [Fact]
    public async Task Will_expire_topic_actor_after_idle()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("batching-producer-test", 20);
            const string topic = "batching-producer";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds).ConfigureAwait(false);

            var firstCluster = _fixture.Members.First();

            var producer = firstCluster
                .BatchingProducer(topic, new BatchingProducerConfig { PublisherIdleTimeout = TimeSpan.FromSeconds(2) });
            await using var _ = producer.ConfigureAwait(false);

            var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
            await Task.WhenAll(tasks).ConfigureAwait(false);

            var pid = await firstCluster.GetAsync(ClusterIdentity.Create(topic, TopicActor.Kind),
                CancellationTokens.FromSeconds(2)).ConfigureAwait(false);
            Assert.NotNull(pid);

            await Task.Delay(TimeSpan.FromSeconds(5)).ConfigureAwait(false);

            var newPid = await firstCluster.GetAsync(ClusterIdentity.Create(topic, TopicActor.Kind),
                CancellationTokens.FromSeconds(2)).ConfigureAwait(false);
            Assert.NotEqual(newPid, pid);
        }).ConfigureAwait(false);
    }

    private void Log(string message) => _output.WriteLine($"[{DateTime.Now:hh:mm:ss.fff}] {message}");
}
