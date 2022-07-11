// -----------------------------------------------------------------------
// <copyright file = "PubSubMemberTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using FluentAssertions;
using Xunit;
using static Proto.Cluster.PubSub.Tests.WaitHelper;

namespace Proto.Cluster.PubSub.Tests;

[Collection("PubSub")] // do not run tests using the cluster in parallel - that fails in CI
public class PubSubMemberTests : IAsyncLifetime
{
    private readonly PubSubClusterFixture _fixture;

    public PubSubMemberTests() => _fixture = new PubSubClusterFixture();

    public Task InitializeAsync() => _fixture.InitializeAsync();

    [Fact]
    public async Task When_member_leaves_PID_subscribers_get_removed_from_the_subscriber_list()
    {
        const string topic = "leaving-member";

        // pid subscriber
        var props = Props.FromFunc(ctx => {
                if (ctx.Message is DataPublished msg)
                    _fixture.Deliveries.Add(new Delivery(ctx.Self.ToDiagnosticString(), msg.Data));
                return Task.CompletedTask;
            }
        );

        // spawn on members
        var leavingMember = _fixture.Members.First();
        var leavingPid = leavingMember.System.Root.Spawn(props);
        var stayingMember = _fixture.Members.Last();
        var stayingPid = stayingMember.System.Root.Spawn(props);

        
        // subscribe by pids
        await leavingMember.Subscribe(topic, leavingPid);
        await stayingMember.Subscribe(topic, stayingPid);

        // to spice things up, also subscribe virtual actors
        var subscriberIds = SubscriberIds("leaving", 20);
        await _fixture.SubscribeAllTo(topic, subscriberIds);

        // publish data
        await _fixture.PublishData(topic, 1);
        
        // everyone should have received the data
        _fixture.Deliveries.Count.Should().Be(subscriberIds.Length + 2);

        // a member leaves - wait of it to make it to the block list
        await _fixture.RemoveNode(leavingMember);
        await WaitUntil(() => _fixture.Members.All(m => m.Remote.BlockList.BlockedMembers.Count == 1));

        // publish again
        _fixture.Deliveries.Clear();
        var publishResp = await _fixture.PublishData(topic, 2);

        // we should be informed about failed delivery because one of the subscribers is gone
        publishResp.Should().BeEquivalentTo(new PublishResponse
            {
                Status = PublishStatus.Failed,
                FailureReason = PublishFailureReason.AtLeastOneMemberLeftTheCluster
            },
            "delivery should fail because one of the subscribers is gone"
        );

        // the failure in delivery caused topic actor to remove subscribers from the member that left
        // next publish should succeed
        _fixture.Deliveries.Clear();
        await _fixture.PublishData(topic, 3);
        
        _fixture.Deliveries.Count.Should().Be(subscriberIds.Length + 1);
    }

    private string[] SubscriberIds(string prefix, int count) => Enumerable.Range(1, count).Select(i => $"{prefix}-{i:D4}").ToArray();

    public Task DisposeAsync() => _fixture.DisposeAsync();
}