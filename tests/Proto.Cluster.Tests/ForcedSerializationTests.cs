// -----------------------------------------------------------------------
// <copyright file = "ForcedSerializationTests.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Gossip;
using Proto.Remote;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class ForcedSerializationTests
{
    [Fact]
    public async Task Forced_serialization_works_correctly_in_a_cluster()
    {
        var fixture = new ForcedSerializationClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();
        var entryMember = fixture.Members[0];

        var testData = Enumerable.Range(1, 100).Select(i => i.ToString()).ToList();

        var tasks = testData.ConvertAll(id => entryMember.Ping(id, id, CancellationTokens.FromSeconds(10)));
        await Task.WhenAll(tasks);

        var results = tasks.ConvertAll(t => t.Result.Message);

        results.Should().BeEquivalentTo(testData);
    }

    [Fact]
    public void The_test_messages_are_allowed_by_the_default_predicate()
    {
        var predicate = ForcedSerializationSenderMiddleware.SkipInternalProtoMessages;

        predicate(MessageEnvelope.Wrap(new Ping())).Should().BeTrue();
    }

    [Fact]
    public void Sample_internal_proto_messages_are_not_allowed_by_the_default_predicate()
    {
        var predicate = ForcedSerializationSenderMiddleware.SkipInternalProtoMessages;

        predicate(MessageEnvelope.Wrap(new GetGossipStateRequest("test"))).Should().BeFalse();
        predicate(MessageEnvelope.Wrap(new GossipState())).Should().BeFalse();
    }

    private class ForcedSerializationClusterFixture : InMemoryClusterFixture
    {
        protected override ActorSystemConfig GetActorSystemConfig() =>
            base.GetActorSystemConfig()
                .WithConfigureRootContext(
                    conf => conf.WithSenderMiddleware(ForcedSerializationSenderMiddleware.Create())
                );
    }
}