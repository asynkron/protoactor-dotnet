// -----------------------------------------------------------------------
// <copyright file="PubSubMemberDeliveryActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PubSub;

public class PubSubMemberDeliveryActor : IActor
{
    private static readonly ShouldThrottle LogThrottle = Throttle.Create(10, TimeSpan.FromSeconds(1));
    private static readonly ILogger Logger = Log.CreateLogger<PubSubMemberDeliveryActor>();

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

            context.ReenterAfter(Task.WhenAll(tasks), () => ReportDelivery(tasks, context));
        }

        return Task.CompletedTask;
    }

    private void ReportDelivery(IEnumerable<Task<SubscriberDeliveryReport>> tasks, IContext context)
    {
        var invalidDeliveries = tasks
            .Select(t => t.Result)
            .Where(t => t.Status != DeliveryStatus.Delivered);

        context.Respond(new DeliverBatchResponse {InvalidDeliveries = {invalidDeliveries}});
    }

    private static async Task<SubscriberDeliveryReport> DeliverBatch(IContext context, PubSubAutoRespondBatch pub, SubscriberIdentity s)
    {
        var status = await (s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid             => DeliverToPid(context, pub, s.Pid),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => DeliverToClusterIdentity(context, pub, s.ClusterIdentity),
            _                                                    => Task.FromResult(DeliveryStatus.OtherError)
        });

        return new SubscriberDeliveryReport {Subscriber = s, Status = status};
    }

    private static async Task<DeliveryStatus> DeliverToClusterIdentity(IContext context, PubSubAutoRespondBatch pub, ClusterIdentity ci)
    {
        try
        {
            // deliver to virtual actor
            // delivery should always be possible, since a virtual actor always exists
            await context.ClusterRequestAsync<object>(ci.Identity, ci.Kind, pub, CancellationToken.None);
            return DeliveryStatus.Delivered;
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            if (LogThrottle().IsOpen())
                Logger.LogError(e, "Error while delivering pub-sub message to {ClusterIdentity}", ci.ToDiagnosticString());

            return DeliveryStatus.OtherError;
        }
    }

    private static async Task<DeliveryStatus> DeliverToPid(IContext context, PubSubAutoRespondBatch pub, PID pid)
    {
        try
        {
            // deliver to PID
            await context.RequestAsync<PublishResponse>(pid, pub);
            return DeliveryStatus.Delivered;
        }
        catch (DeadLetterException)
        {
            if (LogThrottle().IsOpen())
                Logger.LogWarning("Pub-sub message cannot be delivered to {PID} as it is no longer available", pid.ToDiagnosticString());
            return DeliveryStatus.SubscriberNoLongerReachable;
        }
        catch (Exception e)
        {
            e.CheckFailFast();
            if (LogThrottle().IsOpen())
                Logger.LogError(e, "Error while delivering pub-sub message to {PID}", pid.ToDiagnosticString());
            return DeliveryStatus.OtherError;
        }
    }
}