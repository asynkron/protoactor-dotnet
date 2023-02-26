// -----------------------------------------------------------------------
// <copyright file = "PubSubDefaultTopicRegistrationTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using FluentAssertions;
using Xunit;

namespace Proto.Cluster.PubSub.Tests;

[Collection("PubSub")] // The CI is just to slow to run cluster fixture based tests in parallel
public class PubSubDefaultTopicRegistrationTests : IAsyncLifetime
{
    private readonly PubSubClusterFixture _fixture;

    public PubSubDefaultTopicRegistrationTests()
    {
        _fixture = new PubSubClusterFixture(1, true);
    }

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task Pub_sub_works_with_default_topic_registration()
    {
        var subscriberIds = _fixture.SubscriberIds("topic-default", 20);
        const string topic = "topic-default-registration";
        const int numMessages = 100;

        await _fixture.SubscribeAllTo(topic, subscriberIds);

        for (var i = 0; i < numMessages; i++)
        {
            var response = await _fixture.PublishData(topic, i);
            response.Should().NotBeNull("publishing should not time out");
            response!.Status.Should().Be(PublishStatus.Ok);
        }

        await _fixture.VerifyAllSubscribersGotAllTheData(subscriberIds, numMessages);
    }
}