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
using Proto.Utils;
using  Microsoft.Extensions.Logging;

namespace Proto.Cluster.PubSub
{
    public class TopicActor : IActor
    {
        private static ILogger Logger = Log.CreateLogger<TopicActor>();
        private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
        private string _topic = string.Empty;
        private readonly IKeyValueStore<Subscribers> _subscriptionStore;

        public TopicActor(IKeyValueStore<Subscribers> subscriptionStore)
        {
            _subscriptionStore = subscriptionStore;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            ClusterInit ci             => OnClusterInit(context, ci),
            SubscribeRequest sub       => OnSubscribe(context, sub),
            UnsubscribeRequest unsub   => OnUnsubscribe(context, unsub),
            ProducerBatchMessage batch => OnProducerBatch(context, batch),
            _                          => Task.CompletedTask,
        };

        private async Task OnProducerBatch(IContext context, ProducerBatchMessage batch)
        {
            var topicBatch = new TopicBatchMessage(batch.Envelopes);

            var pidTasks =  _subscribers.Select(s => GetPid(context, s)).ToList();
            var subscribers = await Task.WhenAll(pidTasks);
            var members = subscribers.GroupBy(subscriber => subscriber.pid.Address);

            foreach (var member in members)
            {
                var address = member.Key;
                var subscribersOnMember = new Subscribers()
                {
                    Subscribers_ = {member.Select(s => s.subscriber).ToArray()}
                };
                
                var deliveryMessage = new DeliveryBatchMessage(subscribersOnMember, new ProducerBatchMessage());
            }
            //request async all messages to their subscribers
            var tasks =
                _subscribers.Select(sub => DeliverBatch(context, topicBatch, sub));

            //wait for completion
            await Task.WhenAll(tasks);
            
            //ack back to producer
            context.Respond(new PublishResponse());
        }
        
        private static Task<(SubscriberIdentity subscriber, PID pid)> GetPid(IContext context, SubscriberIdentity s) => s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid             => Task.FromResult((s, s.Pid)),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => GetClusterIdentityPid(context, s)!,
            _                                                    => throw new ArgumentOutOfRangeException()
        };

        private static async Task<(SubscriberIdentity, PID)> GetClusterIdentityPid(IContext context, SubscriberIdentity s)
        {
            var pid = await context.Cluster().GetAsync(s.ClusterIdentity.Identity, s.ClusterIdentity.Kind, CancellationToken.None);
            return (s, pid);
        }

        private static Task DeliverBatch(IContext context, TopicBatchMessage pub, SubscriberIdentity s) => s.IdentityCase switch
        {
            SubscriberIdentity.IdentityOneofCase.Pid             => DeliverToPid(context, pub, s.Pid),
            SubscriberIdentity.IdentityOneofCase.ClusterIdentity => DeliverToClusterIdentity(context, pub, s.ClusterIdentity),
            _                                                    => Task.CompletedTask
        };

        private static Task DeliverToClusterIdentity(IContext context, TopicBatchMessage pub, ClusterIdentity ci) =>
            //deliver to virtual actor
            context.ClusterRequestAsync<PublishResponse>(ci.Identity,ci.Kind, pub,
            CancellationToken.None
        );

        private static Task DeliverToPid(IContext context, TopicBatchMessage pub, PID pid) =>
            //deliver to PID
            context.RequestAsync<PublishResponse>(pid, pub);

        private async Task OnClusterInit(IContext context, ClusterInit ci)
        {
            _topic = ci.Identity;
            var subs = await LoadSubscriptions(_topic);
            _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
            Logger.LogInformation("Topic {Topic} started", _topic);
        }

        protected virtual async Task<Subscribers> LoadSubscriptions(string topic)
        { 
            //TODO: cancellation token config?
            var state = await _subscriptionStore.GetAsync(topic, CancellationToken.None);
            Logger.LogInformation("Topic {Topic} loaded subscriptions {Subscriptions}",_topic,state);
            return state;
        }

        protected virtual async Task SaveSubscriptions(string topic, Subscribers subs)
        {
            //TODO: cancellation token config?
            Logger.LogInformation("Topic {Topic} saved subscriptions {Subscriptions}",_topic,subs);
            await _subscriptionStore.SetAsync(topic, subs, CancellationToken.None);
        }

        private async Task OnUnsubscribe(IContext context, UnsubscribeRequest unsub)
        {
            _subscribers = _subscribers.Remove(unsub.Subscriber);
            Logger.LogInformation("Topic {Topic} - {Subscriber} unsubscribed",_topic,unsub);
            await SaveSubscriptions(_topic, new Subscribers() {Subscribers_ = {_subscribers}});
            context.Respond(new UnsubscribeResponse());
        }

        private async Task OnSubscribe(IContext context, SubscribeRequest sub)
        {
            _subscribers = _subscribers.Add(sub.Subscriber);
            Logger.LogInformation("Topic {Topic} - {Subscriber} subscribed",_topic,sub);
            await SaveSubscriptions(_topic, new Subscribers() {Subscribers_ = {_subscribers}});
            context.Respond(new SubscribeResponse());
        }
    }
}