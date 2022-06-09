// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Utils;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.PubSub;

public sealed class TopicActor : IActor
{
    private static readonly ShouldThrottle LogThrottle = Throttle.Create(10, TimeSpan.FromSeconds(1));

    public const string Kind = "prototopic"; // only alphanum in the name, to maximize chances it works on all clustering providers

    private static readonly ILogger Logger = Log.CreateLogger<TopicActor>();
    private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
    private string _topic = string.Empty;
    private readonly IKeyValueStore<Subscribers> _subscriptionStore;

    public TopicActor(IKeyValueStore<Subscribers> subscriptionStore) => _subscriptionStore = subscriptionStore;

    public Task ReceiveAsync(IContext context) => context.Message switch
    {
        Started _                   => OnClusterInit(context),
        SubscribeRequest sub        => OnSubscribe(context, sub),
        UnsubscribeRequest unsub    => OnUnsubscribe(context, unsub),
        PublisherBatchMessage batch => OnProducerBatch(context, batch),
        _                           => Task.CompletedTask,
    };

    private async Task OnProducerBatch(IContext context, PublisherBatchMessage batch)
    {
        //TODO: lookup PID for ClusterIdentity subscribers.
        //group PIDs by address
        //send the batch to the PubSub delivery actor on each member
        //await for subscriber responses on in each delivery actor
        //when done, respond back here

        var pidTasks = _subscribers.Select(s => GetPid(context, s)).ToList();
        var subscribers = await Task.WhenAll(pidTasks);
        var members = subscribers.GroupBy(subscriber => subscriber.pid.Address);

        var acks =
            (from member in members
             let address = member.Key
             let subscribersOnMember = GetSubscribersForAddress(member)
             let deliveryMessage = new DeliveryBatchMessage(subscribersOnMember, batch)
             let deliveryPid = PID.FromAddress(address, PubSubExtension.PubSubDeliveryName)
             select context.RequestAsync<DeliverBatchResponse>(deliveryPid, deliveryMessage))
            .ToList();

        await Task.WhenAll(acks);

        var allInvalidDeliveryReports = acks.SelectMany(a => a.Result.InvalidDeliveries).ToArray();

        if (allInvalidDeliveryReports.Length > 0)
            await HandleInvalidDeliveries(context, allInvalidDeliveryReports);

        // ack back to publisher
        context.Respond(new PublishResponse());
    }

    private async Task HandleInvalidDeliveries(IContext context, SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        await UnsubscribeUnreachableSubscribers(allInvalidDeliveryReports);
        ThrowIfAnyErrorsDuringDelivery(allInvalidDeliveryReports);
    }

    private void ThrowIfAnyErrorsDuringDelivery(SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        var allWithError = allInvalidDeliveryReports
            .Where(r => r.Status == DeliveryStatus.OtherError)
            .ToArray();

        if (allWithError.Length > 0)
        {
            var diagnosticMessage = allWithError
                .Aggregate($"Topic = {_topic} following subscribers could not process the batch: ", (acc, report) => acc + report.Subscriber + ", ");

            throw new PubSubDeliveryException(diagnosticMessage);
        }
    }

    private async Task UnsubscribeUnreachableSubscribers(SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        var allUnreachable = allInvalidDeliveryReports
            .Where(r => r.Status == DeliveryStatus.SubscriberNoLongerReachable)
            .ToArray();

        if (allUnreachable.Length > 0)
        {
            foreach (var report in allUnreachable)
            {
                _subscribers = _subscribers.Remove(report.Subscriber);
            }

            if (LogThrottle().IsOpen())
            {
                var diagnosticMessage = allUnreachable
                    .Aggregate($"Topic = {_topic} removed subscribers, because they are no longer reachable: ", (acc, report) => acc + report.Subscriber + ", ");
                Logger.LogWarning(diagnosticMessage);
            }

            await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
        }
    }

    private static Subscribers GetSubscribersForAddress(IGrouping<string, (SubscriberIdentity subscriber, PID pid)> member) => new()
    {
        Subscribers_ =
        {
            member.Select(s => s.subscriber).ToArray()
        }
    };

    private static Task<(SubscriberIdentity subscriber, PID pid)> GetPid(IContext context, SubscriberIdentity s) => s.IdentityCase switch
    {
        SubscriberIdentity.IdentityOneofCase.Pid             => Task.FromResult((s, s.Pid)),
        SubscriberIdentity.IdentityOneofCase.ClusterIdentity => GetClusterIdentityPid(context, s),
        _                                                    => throw new ArgumentOutOfRangeException()
    };

    private static async Task<(SubscriberIdentity, PID)> GetClusterIdentityPid(IContext context, SubscriberIdentity s)
    {
        var pid = await context.Cluster().GetAsync(s.ClusterIdentity.Identity, s.ClusterIdentity.Kind, CancellationToken.None);
        return (s, pid);
    }

    private async Task OnClusterInit(IContext context)
    {
        _topic = context.Get<ClusterIdentity>()!.Identity;
        var subs = await LoadSubscriptions(_topic);

        if (subs?.Subscribers_ is not null)
        {
            _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
        }

        Logger.LogDebug("Topic {Topic} started", _topic);
    }

    private async Task<Subscribers> LoadSubscriptions(string topic)
    {
        //TODO: cancellation token config?
        var state = await _subscriptionStore.GetAsync(topic, CancellationToken.None);
        Logger.LogDebug("Topic {Topic} loaded subscriptions {Subscriptions}", _topic, state);
        return state;
    }

    private async Task SaveSubscriptions(string topic, Subscribers subs)
    {
        //TODO: cancellation token config?
        Logger.LogDebug("Topic {Topic} saved subscriptions {Subscriptions}", _topic, subs);
        await _subscriptionStore.SetAsync(topic, subs, CancellationToken.None);
    }

    private async Task OnUnsubscribe(IContext context, UnsubscribeRequest unsub)
    {
        _subscribers = _subscribers.Remove(unsub.Subscriber);
        Logger.LogDebug("Topic {Topic} - {Subscriber} unsubscribed", _topic, unsub);
        await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
        context.Respond(new UnsubscribeResponse());
    }

    private async Task OnSubscribe(IContext context, SubscribeRequest sub)
    {
        _subscribers = _subscribers.Add(sub.Subscriber);
        Logger.LogDebug("Topic {Topic} - {Subscriber} subscribed", _topic, sub);
        await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
        context.Respond(new SubscribeResponse());
    }
}