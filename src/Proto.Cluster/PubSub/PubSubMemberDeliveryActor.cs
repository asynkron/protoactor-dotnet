// -----------------------------------------------------------------------
// <copyright file="PubSubMemberDeliveryActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class PubSubMemberDeliveryActor : IActor
    {
        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is DeliveryBatchMessage deliveryBatch)
            {
                // Console.WriteLine("got messages " + deliveryBatch.subscribers.Subscribers_.Count);
                var topicBatch = new TopicBatchMessage(deliveryBatch.ProducerBatch.Envelopes);
                var tasks =
                    deliveryBatch
                        .subscribers.Subscribers_
                        .Select(sub => DeliverBatch(context, topicBatch, sub));
                
                //wait for completion
                await Task.WhenAll(tasks);
                
                context.Respond(new PublishResponse());
            }
        }
        
        private static Task DeliverBatch(IContext context, TopicBatchMessage pub, SubscriberIdentity s) =>
            s.IdentityCase switch
            {
                SubscriberIdentity.IdentityOneofCase.Pid             => DeliverToPid(context, pub, s.Pid),
                SubscriberIdentity.IdentityOneofCase.ClusterIdentity => DeliverToClusterIdentity(context, pub, s.ClusterIdentity),
                _                                                    => Task.CompletedTask
            };

        private static Task DeliverToClusterIdentity(IContext context, TopicBatchMessage pub, ClusterIdentity ci) =>
            //deliver to virtual actor
            context.ClusterRequestAsync<PublishResponse>(ci.Identity,ci.Kind, pub, CancellationToken.None
            );

        private static Task DeliverToPid(IContext context, TopicBatchMessage pub, PID pid) =>
            //deliver to PID
            context.RequestAsync<PublishResponse>(pid, pub);
    }
}