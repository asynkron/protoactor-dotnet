// -----------------------------------------------------------------------
// <copyright file = "PubSubClientTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using FluentAssertions;
using Xunit;
using static Proto.Cluster.PubSub.Tests.WaitHelper;

namespace Proto.Cluster.PubSub.Tests;

[Collection("PubSub")] // The CI is just to slow to run cluster fixture based tests in parallel
public class PubSubClientTests : IAsyncLifetime
{
	private readonly PubSubClusterFixture _fixture;

	public PubSubClientTests()
	{
		_fixture = new PubSubClusterFixture();
	}

	public async Task InitializeAsync()
	{
		await _fixture.SpawnMember();
		await _fixture.SpawnClient();
	}

	public Task DisposeAsync() => _fixture.DisposeAsync();

	[Fact]
	public async Task When_client_leaves_PID_subscribers_get_removed_due_to_dead_letter()
	{
		const string topic = "leaving-client";

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

		var client = _fixture.Clients.First();
		var clientPid = client.System.Root.Spawn(props);
		await client.Subscribe(topic, clientPid);
        
		var subscribers = await _fixture.GetSubscribersForTopic(topic);
		subscribers.Subscribers_.Count.Should().Be(1);
		subscribers.Subscribers_.Should().Contain(s => s.Pid.Equals(clientPid));
        
		// message should send
		await _fixture.PublishData(topic, 1);
        
		await WaitUntil(() => _fixture.Deliveries.Count == 1);
		_fixture.Deliveries.Count.Should().Be(1);
		_fixture.Deliveries.Should().ContainSingle(d => d.Data == 1);

		// shutdown client
		await _fixture.RemoveNode(client, graceful: true);
        
		// subscription should remain, because it never unsubscribed, and there was no topology change
		subscribers = await _fixture.GetSubscribersForTopic(topic);
		subscribers.Subscribers_.Count.Should().Be(1);
		subscribers.Subscribers_.Should().Contain(s => s.Pid.Equals(clientPid));
        
		// messages should not send or attempt to send
		await _fixture.PublishData(topic, 2);
		await Task.Delay(3000);
		await _fixture.PublishData(topic, 2);
	
		// dead letter should be received, so the subscription is removed	
		await WaitUntil(async () =>
		{
			subscribers = await _fixture.GetSubscribersForTopic(topic);
			return subscribers.Subscribers_.Count == 0;
		});
		
		_fixture.Deliveries.Count.Should().Be(1);
		_fixture.Deliveries.Should().ContainSingle(d => d.Data == 1);
	}
}