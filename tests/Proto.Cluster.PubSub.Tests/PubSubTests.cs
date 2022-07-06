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

namespace Proto.Cluster.PubSub.Tests;

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
        var subscriberIds = _fixture.SubscriberIds("single-test", 20);
        const string topic = "single-test-topic";
        const int numMessages = 100;

        await _fixture.SubscribeAllTo(topic, subscriberIds);

        for (var i = 0; i < numMessages; i++)
        {
            var response = await _fixture.PublishData(topic, i);
            if (response == null)
                _output.WriteLine(await _fixture.Members.DumpClusterState());
            response.Should().NotBeNull("publishing should not time out");
            response!.Status.Should().Be(PublishStatus.Ok);
        }

        await _fixture.UnsubscribeAllFrom(topic, subscriberIds);

        _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

    [Fact]
    public async Task Can_deliver_message_batches()
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
                _output.WriteLine(await _fixture.Members.DumpClusterState());
            response.Should().NotBeNull("publishing should not time out");
        }

        await _fixture.UnsubscribeAllFrom(topic, subscriberIds);

        _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

    [Fact]
    public async Task Unsubscribed_actor_does_not_receive_messages()
    {
        const string sub1 = "unsubscribe-test-1";
        const string sub2 = "unsubscribe-test-2";
        const string topic = "unsubscribe-test";

        await _fixture.SubscribeTo(topic, sub1);
        await _fixture.SubscribeTo(topic, sub2);

        await _fixture.UnsubscribeFrom(topic, sub2);

        await _fixture.PublishData(topic, 1);

        _fixture.Deliveries.Should().HaveCount(1, "only one delivery should happen because the other actor is unsubscribed");
        _fixture.Deliveries.First().Identity.Should().Be(sub1, "the other actor should be unsubscribed");
    }

    [Fact]
    public async Task Can_subscribe_with_PID()
    {
        const string topic = "pid-subscribe";

        DataPublished? deliveredMessage = null;

        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished d) deliveredMessage = d;
                return Task.CompletedTask;
            }
        );

        var member = _fixture.Members.First();
        var pid = member.System.Root.Spawn(props);
        await member.Subscribe(topic, pid);

        await _fixture.PublishData(topic, 1);

        await member.Unsubscribe(topic, pid);

        deliveredMessage.Should().BeEquivalentTo(new DataPublished(1));
    }

    [Fact]
    public async Task Can_unsubscribe_with_PID()
    {
        const string topic = "pid-unsubscribe";

        var deliveryCount = 0;

        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished) Interlocked.Increment(ref deliveryCount);
                return Task.CompletedTask;
            }
        );

        var member = _fixture.Members.First();
        var pid = member.System.Root.Spawn(props);

        await member.Subscribe(topic, pid);
        await member.Unsubscribe(topic, pid);

        await _fixture.PublishData(topic, 1);

        deliveryCount.Should().Be(0);
    }

    [Fact]
    public async Task Stopped_actor_that_did_not_unsubscribe_does_not_block_publishing_to_topic()
    {
        const string topic = "missing-unsubscribe";

        var deliveryCount = 0;

        // this scenario is only relevant for regular actors,
        // virtual actors always exist, so the msgs should never be deadlettered 
        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished) Interlocked.Increment(ref deliveryCount);
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

        // kill one of the actors
        await member.System.Root.StopAsync(pid2);

        // publish again
        var response = await _fixture.PublishData(topic, 2);

        // clean up and assert
        await member.Unsubscribe(topic, pid1, CancellationToken.None);

        response.Should().NotBeNull("the publish operation shouldn't have timed out");
        deliveryCount.Should().Be(3, "second publish should be delivered only to one of the actors");
    }

    [Fact]
    public async Task Slow_PID_subscriber_that_times_out_results_in_failed_publish()
    {
        const string topic = "slow-pid-subscriber";

        // a slow subscriber that times out
        var props = Props.FromFunc(ctx => Task.Delay(15000, _fixture.CancelWhenDisposing));

        // subscribe
        var member = _fixture.RandomMember();
        var pid = member.System.Root.Spawn(props);
        await member.Subscribe(topic, pid);

        // publish one message
        var response = await _fixture.PublishData(topic, 1);

        response.Should().BeEquivalentTo(
            new PublishResponse {Status = PublishStatus.Failed},
            "topic actor should return a response indicating failure"
        );
    }

    [Fact]
    public async Task Slow_ClusterIdentity_subscriber_that_times_out_results_in_failed_publish()
    {
        const string topic = "slow-ci-subscriber";

        // subscribe
        await _fixture.SubscribeTo(topic, "slow-ci-1", PubSubClusterFixture.TimeoutSubscriberKind);

        // publish one message
        var response = await _fixture.PublishData(topic, 1);

        response.Should().BeEquivalentTo(
            new PublishResponse {Status = PublishStatus.Failed},
            "topic actor should return a response indicating failure"
        );
    }

    [Fact]
    [SuppressMessage("ReSharper", "AccessToDisposedClosure")]
    public async Task Can_publish_messages_via_batching_producer()
    {
        var subscriberIds = _fixture.SubscriberIds("batching-producer-test", 20);
        const string topic = "batching-producer";
        const int numMessages = 100;

        await _fixture.SubscribeAllTo(topic, subscriberIds);

        await using var producer = _fixture.Members.First().BatchingProducer(topic, new BatchingProducerConfig {BatchSize = 10});

        var tasks = Enumerable.Range(0, numMessages).Select(i => producer.ProduceAsync(new DataPublished(i)));
        await Task.WhenAll(tasks);

        await _fixture.UnsubscribeAllFrom(topic, subscriberIds);

        _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }

}