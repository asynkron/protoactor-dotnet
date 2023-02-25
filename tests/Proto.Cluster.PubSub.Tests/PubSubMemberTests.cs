// -----------------------------------------------------------------------
// <copyright file = "PubSubMemberTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using FluentAssertions;
using Xunit;
using static Proto.Cluster.PubSub.Tests.WaitHelper;

namespace Proto.Cluster.PubSub.Tests;

[Collection("PubSub")] // The CI is just to slow to run cluster fixture based tests in parallel
public class PubSubMemberTests : IAsyncLifetime
{
    private readonly PubSubClusterFixture _fixture;

    public PubSubMemberTests()
    {
        _fixture = new PubSubClusterFixture();
    }

    public Task InitializeAsync() => _fixture.InitializeAsync();

    public Task DisposeAsync() => _fixture.DisposeAsync();

    [Fact]
    public async Task When_member_leaves_PID_subscribers_get_removed_from_the_subscriber_list()
    {
        const string topic = "leaving-member";

        // pid subscriber
        var props = Props.FromFunc(ctx =>
            {
                if (ctx.Message is DataPublished msg)
                {
                    _fixture.Deliveries.Add(new Delivery(ctx.Self.ToDiagnosticString(), msg.Data));
                }

                return Task.CompletedTask;
            }
        );

        // spawn on members
        var leavingMember = _fixture.Members.First();
        var leavingPid = leavingMember.System.Root.Spawn(props);
        var stayingMember = _fixture.Members.Last();
        var stayingPid = stayingMember.System.Root.Spawn(props);

        // subscribe by pids
        await leavingMember.Subscribe(topic, leavingPid).ConfigureAwait(false);
        await stayingMember.Subscribe(topic, stayingPid).ConfigureAwait(false);

        // to spice things up, also subscribe virtual actors
        var subscriberIds = SubscriberIds("leaving", 20);
        await _fixture.SubscribeAllTo(topic, subscriberIds).ConfigureAwait(false);

        // publish data
        await _fixture.PublishData(topic, 1).ConfigureAwait(false);

        // everyone should have received the data
        await WaitUntil(() => _fixture.Deliveries.Count == subscriberIds.Length + 2,
            "All subscribers should get the message").ConfigureAwait(false);

        _fixture.Deliveries.Count.Should().Be(subscriberIds.Length + 2);

        // a member leaves - wait of it to make it to the block list
        await _fixture.RemoveNode(leavingMember).ConfigureAwait(false);

        await WaitUntil(() => _fixture.Members.All(m => m.Remote.BlockList.BlockedMembers.Count == 1),
            "Member should leave cluster").ConfigureAwait(false);

        // publish again
        _fixture.Deliveries.Clear();
        await _fixture.PublishData(topic, 2).ConfigureAwait(false);

        // the failure in delivery caused topic actor to remove subscribers from the member that left
        // next publish should succeed and deliver to remaining subscribers
        await WaitUntil(() => _fixture.Deliveries.Count == subscriberIds.Length + 1,
            "All subscribers apart the one that left should get the message"
        ).ConfigureAwait(false);

        // the subscriber that left should be removed from subscribers list
        await WaitUntil(async () =>
            {
                var subscribers = await _fixture.GetSubscribersForTopic(topic).ConfigureAwait(false);

                return !subscribers.Subscribers_.Contains(new SubscriberIdentity { Pid = leavingPid });
            },
            "Subscriber that left should be removed from subscribers list"
        ).ConfigureAwait(false);
    }

    private string[] SubscriberIds(string prefix, int count) =>
        Enumerable.Range(1, count).Select(i => $"{prefix}-{i:D4}").ToArray();
}