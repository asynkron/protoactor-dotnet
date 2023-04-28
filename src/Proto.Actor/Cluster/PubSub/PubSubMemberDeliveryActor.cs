// -----------------------------------------------------------------------
// <copyright file="PubSubMemberDeliveryActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PubSub;

public class PubSubMemberDeliveryActor : IActor
{
    private static readonly ShouldThrottle LogThrottle = Throttle.Create(10, TimeSpan.FromSeconds(1),
        droppedLogs => Logger?.LogInformation("[PubSubMemberDeliveryActor] Throttled {LogCount} logs", droppedLogs)
    );

    private static readonly ILogger Logger = Log.CreateLogger<PubSubMemberDeliveryActor>();
    private readonly int _subscriberTimeoutSeconds;

    public PubSubMemberDeliveryActor(TimeSpan subscriberTimeout)
    {
        _subscriberTimeoutSeconds = (int)subscriberTimeout.TotalSeconds;
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is DeliverBatchRequest deliveryBatch)
        {
            var topicBatch = new PubSubAutoRespondBatch(deliveryBatch.PubSubBatch.Envelopes);

            var tasks =
                deliveryBatch
                    .Subscribers.Subscribers_
                    .Select(sub => DeliverBatch(context, topicBatch, sub))
                    .ToArray();

            context.ReenterAfter(Task.WhenAll(tasks),
                () => NotifyAboutInvalidDeliveries(tasks, deliveryBatch.Topic, context));
        }

        return Task.CompletedTask;
    }

    private void NotifyAboutInvalidDeliveries(IEnumerable<Task<SubscriberDeliveryReport>> tasks, string topic,
        IContext context)
    {
        var invalidDeliveries = tasks
            .Select(t => t.Result)
            .Where(t => t.Status != DeliveryStatus.Delivered)
            .ToArray();

        if (invalidDeliveries.Length > 0)
        {
            // no need to await here
            // we use cluster.RequestAsync to locate the topic actor in the cluster
            // but we don't care about the result of the request
            _ = context.Cluster()
                .RequestAsync<NotifyAboutFailingSubscribersResponse>(
                    topic,
                    TopicActor.Kind,
                    new NotifyAboutFailingSubscribersRequest
                    {
                        InvalidDeliveries = { invalidDeliveries }
                    }, CancellationTokens.FromSeconds(15)
                );
        }
    }

    private async Task<SubscriberDeliveryReport> DeliverBatch(IContext context, PubSubAutoRespondBatch pub,
        SubscriberIdentity s)
    {
        var status = await (s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid => DeliverToPid(context, pub, s.Pid),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => DeliverToClusterIdentity(context, pub,
                s.ClusterIdentity),
            _ => Task.FromResult(DeliveryStatus.OtherError)
        }).ConfigureAwait(false);

        return new SubscriberDeliveryReport { Subscriber = s, Status = status };
    }

    private async Task<DeliveryStatus> DeliverToClusterIdentity(IContext context, PubSubAutoRespondBatch pub,
        ClusterIdentity ci)
    {
        try
        {
            // deliver to virtual actor
            // delivery should always be possible, since a virtual actor always exists
            var response = await context.ClusterRequestAsync<PublishResponse>(ci.Identity, ci.Kind, pub,
                CancellationTokens.FromSeconds(_subscriberTimeoutSeconds)
            ).ConfigureAwait(false);

            if (response == null)
            {
                if (LogThrottle().IsOpen())
                {
                    Logger.LogWarning("Pub-sub message delivered to {ClusterIdentity} timed out",
                        ci.ToDiagnosticString());
                }

                return DeliveryStatus.Timeout;
            }

            return DeliveryStatus.Delivered;
        }
        catch (TimeoutException)
        {
            if (LogThrottle().IsOpen())
            {
                Logger.LogWarning("Pub-sub message delivered to: {ClusterIdentity} timed out", ci.ToDiagnosticString());
            }

            return DeliveryStatus.Timeout;
        }
        catch (Exception e)
        {
            e.CheckFailFast();

            if (LogThrottle().IsOpen())
            {
                Logger.LogError(e, "Error while delivering pub-sub message to {ClusterIdentity}",
                    ci.ToDiagnosticString());
            }

            return DeliveryStatus.OtherError;
        }
    }

    private async Task<DeliveryStatus> DeliverToPid(IContext context, PubSubAutoRespondBatch pub, PID pid)
    {
        try
        {
            // deliver to PID
            await context.RequestAsync<PublishResponse>(pid, pub,
                CancellationTokens.FromSeconds(_subscriberTimeoutSeconds)).ConfigureAwait(false);

            return DeliveryStatus.Delivered;
        }
        catch (TimeoutException)
        {
            if (LogThrottle().IsOpen())
            {
                Logger.LogWarning("Pub-sub message delivered to {PID} timed out", pid.ToDiagnosticString());
            }

            return DeliveryStatus.Timeout;
        }
        catch (DeadLetterException)
        {
            if (LogThrottle().IsOpen())
            {
                Logger.LogWarning("Pub-sub message cannot be delivered to {PID} as it is no longer available",
                    pid.ToDiagnosticString());
            }

            return DeliveryStatus.SubscriberNoLongerReachable;
        }
        catch (Exception e)
        {
            e.CheckFailFast();

            if (LogThrottle().IsOpen())
            {
                Logger.LogError(e, "Error while delivering pub-sub message to {PID}", pid.ToDiagnosticString());
            }

            return DeliveryStatus.OtherError;
        }
    }
}