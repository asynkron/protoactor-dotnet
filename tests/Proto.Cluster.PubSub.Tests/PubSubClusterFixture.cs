// -----------------------------------------------------------------------
// <copyright file = "PubSubInMemoryClusterFixture.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Concurrent;
using FluentAssertions;
using Proto.Cluster.Tests;
using Xunit.Abstractions;

namespace Proto.Cluster.PubSub.Tests;

public record DataPublished(int Data);

public record Delivery(string Identity, int Data);

public record Response;

public class PubSubClusterFixture : BaseInMemoryClusterFixture
{
    public const string SubscriberKind = "Subscriber";
    public const string TimeoutSubscriberKind = "TimeoutSubscriber";

    public readonly ConcurrentBag<Delivery> Deliveries = new();

    private readonly CancellationTokenSource _cts = new();
    public CancellationToken CancelWhenDisposing => _cts.Token;

    public ITestOutputHelper? Output;

    public PubSubClusterFixture() : base(3, config =>
        config
            .WithPubSubConfig(PubSubConfig.Setup()
                .WithSubscriberTimeout(TimeSpan.FromSeconds(2))
                .WithPublishTimeout(TimeSpan.FromSeconds(5))
            )
    )
    {
    }

    private readonly InMemorySubscribersStore _subscribersStore = new();
    
    public Task<Subscribers> GetSubscribersForTopic(string topic) => _subscribersStore.GetAsync(topic, CancellationToken.None);

    protected override ClusterKind[] ClusterKinds => new[]
    {
        new ClusterKind(TopicActor.Kind, Props.FromProducer(() => new TopicActor(_subscribersStore))),
        new ClusterKind(SubscriberKind, SubscriberProps()),
        new ClusterKind(TimeoutSubscriberKind, TimeoutSubscriberProps())
    };

    private Props SubscriberProps()
    {
        Task Receive(IContext context)
        {
            if (context.Message is DataPublished msg)
            {
                Deliveries.Add(new Delivery(context.ClusterIdentity()!.Identity, msg.Data));
                TestLog.Log?.Invoke($"$ACTOR: {context.ClusterIdentity()!.Identity} got {msg.Data}");
                context.Respond(new Response());
            }

            return Task.CompletedTask;
        }

        return Props.FromFunc(Receive);
    }

    private Props TimeoutSubscriberProps()
    {
        async Task Receive(IContext context)
        {
            if (context.Message is DataPublished msg)
            {
                TestLog.Log?.Invoke($"ACTOR: Got message {context.Message}");
                await Task.Delay(4000, CancelWhenDisposing); // 4 seconds is longer than the configured subscriber timeout
                Deliveries.Add(new Delivery(context.ClusterIdentity()!.Identity, msg.Data));
                context.Respond(new Response());
            }
        }

        return Props.FromFunc(Receive);
    }

    public override Task OnDisposing()
    {
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private readonly Random _random = new();

    public Cluster RandomMember() => Members[_random.Next(Members.Count)];

    public async Task VerifyAllSubscribersGotAllTheData(string[] subscriberIds, int numMessages)
    {
        await WaitHelper.WaitUntil(() => Deliveries.Count == subscriberIds.Length * numMessages, "All messages should be delivered");
        
        var expected = subscriberIds
            .SelectMany(id => Enumerable.Range(0, numMessages).Select(i => new Delivery(id, i)))
            .ToArray();

        var actual = Deliveries.OrderBy(d => d.Identity).ThenBy(d => d.Data).ToArray();

        try
        {
            actual.Should().Equal(expected, "the data published should be received by all subscribers");
        }
        catch
        {
            Output?.WriteLine(actual
                .GroupBy(d => d.Identity)
                .Select(g => (g.Key, Data: g.Aggregate("", (acc, delivery) => acc + delivery.Data + ",")))
                .Aggregate("", (acc, d) => $"{acc}ID: {d.Key}, got: {d.Data}\n")
            );

            throw;
        }
    }

    public async Task SubscribeAllTo(string topic, string[] subscriberIds)
    {
        foreach (var id in subscriberIds)
        {
            await SubscribeTo(topic, id);
        }
    }

    public async Task UnsubscribeAllFrom(string topic, string[] subscriberIds)
    {
        foreach (var id in subscriberIds)
        {
            await UnsubscribeFrom(topic, id);
        }
    }

    public string[] SubscriberIds(string prefix, int count) => Enumerable.Range(1, count).Select(i => $"{prefix}-{i:D4}").ToArray();

    public async Task SubscribeTo(string topic, string identity, string kind = PubSubClusterFixture.SubscriberKind)
    {
        var subRes = await RandomMember().Subscribe(topic, identity, kind);
        if (subRes == null)
            Output?.WriteLine($"{kind}/{identity} failed to subscribe due to timeout");
    }

    public async Task UnsubscribeFrom(string topic, string identity, string kind = PubSubClusterFixture.SubscriberKind)
    {
        var unsubRes = await RandomMember().Unsubscribe(topic, identity, kind);
        if (unsubRes == null)
            Output?.WriteLine($"{kind}/{identity} failed to subscribe due to timeout");
    }

    public Task<PublishResponse?> PublishData(string topic, int data, CancellationToken cancel = default)
        => RandomMember()
            .Publisher()
            .Publish(topic, new DataPublished(data), cancel);

    public Task<PublishResponse?> PublishDataBatch(string topic, int[] data, CancellationToken cancel = default)
        => RandomMember()
            .Publisher()
            .PublishBatch(
                topic,
                data.Select(d => new DataPublished(d)).ToArray(),
                cancel
            );
}