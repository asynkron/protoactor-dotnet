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
    private TimeSpan _memberDeliveryTimeout;

    public TopicActor(IKeyValueStore<Subscribers> subscriptionStore) => _subscriptionStore = subscriptionStore;

    public Task ReceiveAsync(IContext context) => context.Message switch
    {
        Started _                => OnClusterInit(context),
        SubscribeRequest sub     => OnSubscribe(context, sub),
        UnsubscribeRequest unsub => OnUnsubscribe(context, unsub),
        PubSubBatch batch        => OnPubSubBatch(context, batch),
        _                        => Task.CompletedTask,
    };

    private async Task OnPubSubBatch(IContext context, PubSubBatch batch)
    {
        try
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
                 let deliveryMessage = new DeliverBatchRequest(subscribersOnMember, batch)
                 let deliveryPid = PID.FromAddress(address, PubSubExtension.PubSubDeliveryName)
                 select context.RequestAsync<DeliverBatchResponse>(deliveryPid, deliveryMessage, _memberDeliveryTimeout))
                .ToList();

            await Task.WhenAll(acks);

            var allInvalidDeliveryReports = acks.SelectMany(a => a.Result.InvalidDeliveries).ToArray();

            if (allInvalidDeliveryReports.Length > 0)
            {
                await HandleInvalidDeliveries(context, allInvalidDeliveryReports);

                // nack back to publisher
                context.Respond(new PublishResponse
                    {
                        Status = PublishStatus.Failed,
                        FailureReason = PublishFailureReason.AtLeastOneSubscriberUnreachable
                    }
                );
            }
            else
            {
                // ack back to publisher
                context.Respond(new PublishResponse());
            }
        }
        catch (TimeoutException)
        {
            // failed to deliver to at least one member
            // remove PID subscribers that are on members no longer present in the member list
            // the ClusterIdentity subscribers always exist, so no action is needed here
            await UnsubscribeSubscribersOnMembersThatLeft(context);

            // nack back to publisher
            context.Respond(new PublishResponse
                {
                    Status = PublishStatus.Failed,
                    FailureReason = PublishFailureReason.AtLeastOneMemberLeftTheCluster
                }
            );
        }
        catch (Exception e)
        {
            if (LogThrottle().IsOpen())
            {
                Logger.LogWarning(e, "Error when delivering message batch");
            }

            // nack back to publisher
            context.Respond(new PublishResponse
                {
                    Status = PublishStatus.Failed,
                    FailureReason = PublishFailureReason.Unknown
                }
            );
        }
    }

    private async Task HandleInvalidDeliveries(IContext context, SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        await UnsubscribeUnreachablePidSubscribers(allInvalidDeliveryReports);
        LogOtherDeliveryErrors(allInvalidDeliveryReports);
    }

    private void LogOtherDeliveryErrors(SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        var allWithError = allInvalidDeliveryReports
            .Where(r => r.Status == DeliveryStatus.OtherError)
            .ToArray();

        if (allWithError.Length > 0 && LogThrottle().IsOpen())
        {
            var diagnosticMessage = allWithError
                .Aggregate($"Topic = {_topic} following subscribers could not process the batch: ", (acc, report) => acc + report.Subscriber + ", ");
            Logger.LogError(diagnosticMessage);
        }
    }
    
    private async Task UnsubscribeUnreachablePidSubscribers(SubscriberDeliveryReport[] allInvalidDeliveryReports)
    {
        var allUnreachable = allInvalidDeliveryReports
            .Where(r => r is
                {
                    Subscriber.IdentityCase: SubscriberIdentity.IdentityOneofCase.Pid,
                    Status: DeliveryStatus.SubscriberNoLongerReachable or DeliveryStatus.Timeout
                }
            )
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
                    .Aggregate($"Topic = {_topic} removed subscribers, because they are no longer reachable: ",
                        (acc, report) => acc + report.Subscriber + ", "
                    );
                Logger.LogWarning(diagnosticMessage);
            }

            await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
        }
    }

    private async Task UnsubscribeSubscribersOnMembersThatLeft(IContext ctx)
    {
        var activeMemberAddresses = ctx.Cluster().MemberList.GetAllMembers().Select(m => m.Address).ToList();

        var subscribersThatLeft = _subscribers
            .Where(s => s.IdentityCase == SubscriberIdentity.IdentityOneofCase.Pid && !activeMemberAddresses.Contains(s.Pid.Address))
            .ToList();

        foreach (var subscriber in subscribersThatLeft)
        {
            _subscribers = _subscribers.Remove(subscriber);
        }

        if (LogThrottle().IsOpen())
        {
            var diagnosticMessage = subscribersThatLeft
                .Aggregate($"Topic = {_topic} removed subscribers, because they are on members that left the cluster: ",
                    (acc, subscriber) => acc + subscriber + ", "
                );
            Logger.LogWarning(diagnosticMessage);
        }

        await SaveSubscriptions(_topic, new Subscribers {Subscribers_ = {_subscribers}});
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

    private async Task OnClusterInit(IContext context)
    {
        _memberDeliveryTimeout = context.Cluster().Config.PubSubConfig.MemberDeliveryTimeout;
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