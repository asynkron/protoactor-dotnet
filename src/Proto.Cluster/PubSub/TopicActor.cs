// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;
using Proto.Utils.Proto.Utils;

namespace Proto.Cluster.PubSub
{
    public class TopicActor : IActor
    {
        private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
        private string _topic = string.Empty;
        private readonly IKeyValueStore<Subscribers> _subscriptionStore;

        public TopicActor(IKeyValueStore<Subscribers> subscriptionStore)
        {
            _subscriptionStore = subscriptionStore;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            ClusterInit ci           => OnClusterInit(context, ci),
            SubscribeRequest sub     => OnSubscribe(context, sub),
            UnsubscribeRequest unsub => OnUnsubscribe(context, unsub),
            ProducerBatch batch      => OnProducerBatch(context, batch),
            _                        => Task.CompletedTask,
        };

        private async Task OnProducerBatch(IContext context, ProducerBatch batch)
        {
            var s = context.System.Serialization();
            
            //deserialize messages in the envelope
            var messages = batch
                .Envelopes
                .Select(e => s
                    .Deserialize(batch.TypeNames[e.TypeId], e.MessageData, s.DefaultSerializerId))
                .ToList();
            
            //request async all messages to their subscribers
            var tasks =
                (from sub in _subscribers
                 from message in messages
                 select DeliverMessage(context, message, sub))
                .ToList();
                        
            //wait for completion
            await Task.WhenAll(tasks);
            
            //ack back to producer
            context.Respond(new PublishResponse());
        }

        private static Task DeliverMessage(IContext context, object pub, SubscriberIdentity s) => s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid             => DeliverToPid(context, pub, s),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => DeliverToClusterIdentity(context, pub, s),
            _                                                    => Task.CompletedTask
        };

        private static async Task DeliverToClusterIdentity(IContext context, object pub, SubscriberIdentity s)
            => await context.ClusterRequestAsync<PublishResponse>(s.ClusterIdentity.Identity, s.ClusterIdentity.Kind, pub,
                CancellationToken.None
            );

        private static async Task DeliverToPid(IContext context, object pub, SubscriberIdentity s)
            => await context.RequestAsync<PublishResponse>(s.Pid, pub);

        private async Task OnClusterInit(IContext context, ClusterInit ci)
        {
            _topic = ci.Identity;
            var subs = await LoadSubscriptions(_topic);
            _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
        }

        protected virtual async Task<Subscribers> LoadSubscriptions(string topic)
        { 
            //TODO: cancellation token config?
            var state = await _subscriptionStore.GetAsync(topic, CancellationToken.None);
            return state;
        }

        protected virtual async Task SaveSubscriptions(string topic, Subscribers subs)
        {
            //TODO: cancellation token config?
            await _subscriptionStore.SetAsync(topic, subs, CancellationToken.None);
        }

        private async Task OnUnsubscribe(IContext context, UnsubscribeRequest unsub)
        {
            _subscribers = _subscribers.Remove(unsub.Subscriber);
            await SaveSubscriptions(_topic, new Subscribers() {Subscribers_ = {_subscribers}});
            context.Respond(new UnsubscribeResponse());
        }

        private async Task OnSubscribe(IContext context, SubscribeRequest sub)
        {
            Console.WriteLine($"{_topic} - Subscriber attached {sub.Subscriber}");
            _subscribers = _subscribers.Add(sub.Subscriber);
            await SaveSubscriptions(_topic, new Subscribers() {Subscribers_ = {_subscribers}});
            context.Respond(new SubscribeResponse());
        }
    }
}