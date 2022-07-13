// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Utils;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.PubSub;

public sealed class TopicActor : IActor
{
    private static readonly ShouldThrottle LogThrottle = Throttle.Create(10, TimeSpan.FromSeconds(1),
        droppedLogs => Logger?.LogInformation("[TopicActor] Throttled {LogCount} logs", droppedLogs));

    public const string Kind = "prototopic"; // only alphanum in the name, to maximize chances it works on all clustering providers

    private static readonly ILogger Logger = Log.CreateLogger<TopicActor>();
    private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
    private string _topic = string.Empty;
    private readonly IKeyValueStore<Subscribers> _subscriptionStore;
    private EventStreamSubscription<object>? _topologySubscription;

    public TopicActor(IKeyValueStore<Subscribers> subscriptionStore) => _subscriptionStore = subscriptionStore;

    public Task ReceiveAsync(IContext context) => context.Message switch
    {
        Started                                  => OnStarted(context),
        Stopping                                 => OnStopping(context),
        SubscribeRequest sub                     => OnSubscribe(context, sub),
        UnsubscribeRequest unsub                 => OnUnsubscribe(context, unsub),
        PubSubBatch batch                        => OnPubSubBatch(context, batch),
        NotifyAboutFailingSubscribersRequest msg => OnNotifyAboutFailingSubscribers(context, msg),
        ClusterTopology msg                      => OnClusterTopologyChanged(context, msg),
        _                                        => Task.CompletedTask,
    };

    private async Task OnStarted(IContext context)
    {
        _topic = context.Get<ClusterIdentity>()!.Identity;
        _topologySubscription = context.System.EventStream.Subscribe<ClusterTopology>(context, context.Self);

        var subs = await LoadSubscriptions(_topic);

        if (subs.Subscribers_ is not null)
        {
            _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
        }

        await UnsubscribeSubscribersOnMembersThatLeft(context);

        Logger.LogDebug("Topic {Topic} started", _topic);
    }

    private Task OnStopping(IContext _)
    {
        _topologySubscription?.Unsubscribe();
        _topologySubscription = null;
        return Task.CompletedTask;
    }

    private async Task OnPubSubBatch(IContext context, PubSubBatch batch)
    {
        try
        {
            //TODO: lookup PID for ClusterIdentity subscribers.
            //group PIDs by address
            //send the batch to the PubSub delivery actor on each member
            //when done, respond back here

            var pidTasks = _subscribers.Select(s => GetPid(context, s)).ToList();
            var subscribers = await Task.WhenAll(pidTasks);
            var members = subscribers.GroupBy(subscriber => subscriber.pid.Address);

            TestLog.Log?.Invoke($"TOPIC: Delivering message to members, members: {members.Count()}, messages: {batch.Envelopes.Count}");
            var memberDeliveries =
                (from member in members
                 let address = member.Key
                 let subscribersOnMember = GetSubscribersForAddress(member)
                 let deliveryMessage = new DeliverBatchRequest(subscribersOnMember, batch, _topic)
                 let deliveryPid = PID.FromAddress(address, PubSubExtension.PubSubDeliveryName)
                 select (Pid: deliveryPid, Message: deliveryMessage));

            foreach (var md in memberDeliveries)
            {
                context.Send(md.Pid, md.Message);
            }

            context.Respond(new PublishResponse());
        }
        catch (Exception e)
        {
            TestLog.Log?.Invoke($"TOPIC: An error occurred while delivering message to members: {e}");

            if (LogThrottle().IsOpen())
            {
                Logger.LogWarning(e, "Error when delivering message batch");
            }

            // nack back to publisher
            context.Respond(new PublishResponse
                {
                    Status = PublishStatus.Failed
                }
            );
        }
    }

    private static Subscribers GetSubscribersForAddress(IGrouping<string, (SubscriberIdentity subscriber, PID pid)> member)
        => new()
        {
            Subscribers_ =
            {
                member.Select(s => s.subscriber).ToArray()
            }
        };

    private static Task<(SubscriberIdentity subscriber, PID pid)> GetPid(IContext context, SubscriberIdentity s)
        => s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid             => Task.FromResult((s, s.Pid)),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => GetClusterIdentityPid(context, s),
            _                                                    => throw new ArgumentOutOfRangeException()
        };

    private static async Task<(SubscriberIdentity, PID)> GetClusterIdentityPid(IContext context, SubscriberIdentity s)
    {
        // TODO: optimize with caching
        var pid = await context.Cluster().GetAsync(s.ClusterIdentity.Identity, s.ClusterIdentity.Kind, CancellationToken.None);
        return (s, pid);
    }

    private async Task OnNotifyAboutFailingSubscribers(IContext context, NotifyAboutFailingSubscribersRequest msg)
    {
        TestLog.Log?.Invoke($"TOPIC: Got notified about failing subscribers {msg}");

        await UnsubscribeUnreachablePidSubscribers(msg.InvalidDeliveries);
        LogDeliveryErrors(msg.InvalidDeliveries);

        context.Respond(new NotifyAboutFailingSubscribersResponse());
    }

    private void LogDeliveryErrors(IReadOnlyCollection<SubscriberDeliveryReport> allInvalidDeliveryReports)
    {
        if (allInvalidDeliveryReports.Count > 0 && LogThrottle().IsOpen())
        {
            var diagnosticMessage = allInvalidDeliveryReports
                .Aggregate($"Topic = {_topic} following subscribers could not process the batch: ", (acc, report) => acc + report.Subscriber + ", ");
            Logger.LogWarning(diagnosticMessage);
        }
    }

    private async Task UnsubscribeUnreachablePidSubscribers(IReadOnlyCollection<SubscriberDeliveryReport> allInvalidDeliveryReports)
    {
        var allUnreachable = allInvalidDeliveryReports
            .Where(r => r is
                {
                    Subscriber.IdentityCase: SubscriberIdentity.IdentityOneofCase.Pid,
                    Status: DeliveryStatus.SubscriberNoLongerReachable
                }
            )
            .Select(s => s.Subscriber)
            .ToList();

        await RemoveSubscribers(allUnreachable);
    }

    private async Task OnClusterTopologyChanged(IContext context, ClusterTopology topology)
    {
        if (topology.Left.Count > 0)
        {
            var addressesThatLeft = topology.Left.Select(m => m.Address).ToList();

            var subscribersThatLeft = _subscribers
                .Where(s => s.IdentityCase == SubscriberIdentity.IdentityOneofCase.Pid && addressesThatLeft.Contains(s.Pid.Address))
                .ToList();

            await RemoveSubscribers(subscribersThatLeft);
        }
    }

    private async Task UnsubscribeSubscribersOnMembersThatLeft(IContext ctx)
    {
        var activeMemberAddresses = ctx.Cluster().MemberList.GetAllMembers().Select(m => m.Address).ToList();

        var subscribersThatLeft = _subscribers
            .Where(s => s.IdentityCase == SubscriberIdentity.IdentityOneofCase.Pid && !activeMemberAddresses.Contains(s.Pid.Address))
            .ToList();

        await RemoveSubscribers(subscribersThatLeft);
    }

    private async Task RemoveSubscribers(IReadOnlyCollection<SubscriberIdentity> subscribersThatLeft)
    {
        if (subscribersThatLeft.Count > 0)
        {
            foreach (var subscriber in subscribersThatLeft)
            {
                _subscribers = _subscribers.Remove(subscriber);
            }

            if (LogThrottle().IsOpen())
            {
                var diagnosticMessage = subscribersThatLeft
                    .Aggregate(
                        $"Topic = {_topic} removed subscribers, removed subscribers, because they are dead or they are on members that left the cluster: ",
                        (acc, subscriber) => acc + subscriber + ", "
                    );
                Logger.LogWarning(diagnosticMessage);
            }

            await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
        }
    }

    private async Task<Subscribers> LoadSubscriptions(string topic)
    {
        try
        {
            //TODO: cancellation token config?
            var state = await _subscriptionStore.GetAsync(topic, CancellationToken.None);
            Logger.LogDebug("Topic {Topic} loaded subscriptions {Subscriptions}", _topic, state);
            return state;
        }
        catch (Exception e)
        {
            if (LogThrottle().IsOpen())
                Logger.LogError(e, "Error when loading subscriptions");
            return new Subscribers();
        }
    }

    private async Task SaveSubscriptions(string topic, Subscribers subs)
    {
        try
        {
            //TODO: cancellation token config?
            Logger.LogDebug("Topic {Topic} saved subscriptions {Subscriptions}", _topic, subs);
            await _subscriptionStore.SetAsync(topic, subs, CancellationToken.None);
        }
        catch (Exception e)
        {
            if (LogThrottle().IsOpen())
                Logger.LogError(e, "Error when saving subscriptions");
        }
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