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

            await _fixture.SubscribeAllTo(topic, subscriberIds);

            for (var i = 0; i < numMessages; i++)
            {
                var response = await _fixture.PublishData(topic, i);

                if (response == null)
                {
                    _output.WriteLine(await _fixture.Members.DumpClusterState());
                }

                response.Should().NotBeNull("publishing should not time out");
                response!.Status.Should().Be(PublishStatus.Ok);
            }

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
        });
    }

    [Fact]
    public async Task Can_deliver_message_batches()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("batch-test", 20);
            const string topic = "batch-test-topic";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds);

            for (var i = 0; i < numMessages / 10; i++)
            {
                var data = Enumerable.Range(i * 10, 10).ToArray();
                var response = await _fixture.PublishDataBatch(topic, data);

                if (response == null)
                {
                    _output.WriteLine(await _fixture.Members.DumpClusterState());
                }

                response.Should().NotBeNull("publishing should not time out");
            }

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
        });
    }

    [Fact]
    public async Task Unsubscribed_actor_does_not_receive_messages()
    {
        await _fixture.Trace(async () =>
        {
            const string sub1 = "unsubscribe-test-1";
            const string sub2 = "unsubscribe-test-2";
            const string topic = "unsubscribe-test";

            await _fixture.SubscribeTo(topic, sub1);
            await _fixture.SubscribeTo(topic, sub2);

            await _fixture.UnsubscribeFrom(topic, sub2);

            await _fixture.PublishData(topic, 1);
            await Task.Delay(1000); // give time for the message "not to be delivered" to second subscriber

            await WaitUntil(() => _fixture.Deliveries.Count == 1,
                "only one delivery should happen because the other actor is unsubscribed");

            _fixture.Deliveries.Should()
                .HaveCount(1, "only one delivery should happen because the other actor is unsubscribed");

            _fixture.Deliveries.First().Identity.Should().Be(sub1, "the other actor should be unsubscribed");
        });
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
            await member.Subscribe(topic, pid);

            await _fixture.PublishData(topic, 1);

            await WaitUntil(() => deliveredMessage != null, "Message should be delivered");
            deliveredMessage.Should().BeEquivalentTo(new DataPublished(1));
        });
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

            await member.Subscribe(topic, pid);
            await member.Unsubscribe(topic, pid);

            await _fixture.PublishData(topic, 1);
            await Task.Delay(1000); // give time for the message "not to be delivered" to second subscriber

            deliveryCount.Should().Be(0);
        });
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

            await member.Subscribe(topic, pid1);
            await member.Subscribe(topic, pid2);

            // publish one message
            await _fixture.PublishData(topic, 1);
            await WaitUntil(() => deliveryCount == 2, "both messages should be delivered");

            // kill one of the actors
            await member.System.Root.StopAsync(pid2);

            // publish again
            var response = await _fixture.PublishData(topic, 2);

            response.Should().NotBeNull("the publish operation shouldn't have timed out");
            await WaitUntil(() => deliveryCount == 3, "second publish should be delivered only to one of the actors");

            await WaitUntil(async () =>
                {
                    var subscribers = await _fixture.GetSubscribersForTopic(topic);

                    return !subscribers.Subscribers_!.Contains(new SubscriberIdentity { Pid = pid2 });
                }
            );
        });
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
                            .CancelWhenDisposing); // 4 seconds is longer than the subscriber timeout configured in the test fixture

                    Interlocked.Increment(ref deliveryCount);
                }
            );

            // subscribe
            var member = _fixture.RandomMember();
            var pid = member.System.Root.Spawn(props);
            await member.Subscribe(topic, pid);

            // publish one message
            await _fixture.PublishData(topic, 1);

            // next published message should also be delivered
            await _fixture.PublishData(topic, 1);

            await WaitUntil(() => deliveryCount == 2,
                "A timing out subscriber should not prevent subsequent publishes", TimeSpan.FromSeconds(10)
            );
        });
    }

    [Fact]
    public async Task Slow_ClusterIdentity_subscriber_that_times_out_does_not_prevent_subsequent_publishes()
    {
        await _fixture.Trace(async () =>
        {
            const string topic = "slow-ci-subscriber";

            // subscribe
            await _fixture.SubscribeTo(topic, "slow-ci-1", PubSubClusterFixture.TimeoutSubscriberKind);

            // publish one message
            await _fixture.PublishData(topic, 1);

            // next published message should also be delivered
            await _fixture.PublishData(topic, 1);

            await WaitUntil(() => _fixture.Deliveries.Count == 2,
                "A timing out subscriber should not prevent subsequent publishes", TimeSpan.FromSeconds(10)
            );
        });
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

            await _fixture.SubscribeAllTo(topic, subscriberIds);

            var producer = _fixture.Members.First()
                .BatchingProducer(topic, new BatchingProducerConfig { BatchSize = 10 });
            await using var _ = producer;

            var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
            await Task.WhenAll(tasks);

            await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
        });
    }

    [Fact]
    public async Task Will_expire_topic_actor_after_idle()
    {
        await _fixture.Trace(async () =>
        {
            var subscriberIds = _fixture.SubscriberIds("batching-producer-test", 20);
            const string topic = "batching-producer";
            const int numMessages = 100;

            await _fixture.SubscribeAllTo(topic, subscriberIds);

            var firstCluster = _fixture.Members.First();

            var producer = firstCluster
                .BatchingProducer(topic, new BatchingProducerConfig { PublisherIdleTimeout = TimeSpan.FromSeconds(2) });
            await using var _ = producer;

            var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
            await Task.WhenAll(tasks);

            var pid = await firstCluster.GetAsync(ClusterIdentity.Create(topic, TopicActor.Kind),
                CancellationTokens.FromSeconds(2));
            Assert.NotNull(pid);

            await Task.Delay(TimeSpan.FromSeconds(5));

            var newPid = await firstCluster.GetAsync(ClusterIdentity.Create(topic, TopicActor.Kind),
                CancellationTokens.FromSeconds(2));
            Assert.NotEqual(newPid, pid);
        });
    }

    private void Log(string message) => _output.WriteLine($"[{DateTime.Now:hh:mm:ss.fff}] {message}");
}
