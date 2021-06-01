// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Utils;
using  Microsoft.Extensions.Logging;

namespace Proto.Cluster.PubSub
{
    public sealed class TopicActor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<TopicActor>();
        private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
        private string _topic = string.Empty;
        private readonly IKeyValueStore<Subscribers> _subscriptionStore;

        public TopicActor(IKeyValueStore<Subscribers> subscriptionStore)
        {
            _subscriptionStore = subscriptionStore;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            ClusterInit ci             => OnClusterInit(context),
            SubscribeRequest sub       => OnSubscribe(context, sub),
            UnsubscribeRequest unsub   => OnUnsubscribe(context, unsub),
            ProducerBatchMessage batch => OnProducerBatch(context, batch),
            _                          => Task.CompletedTask,
        };

        private async Task OnProducerBatch(IContext context, ProducerBatchMessage batch)
        {
            //TODO: lookup PID for ClusterIdentity subscribers.
            //group PIDs by address
            //send the batch to the PubSub delivery actor on each member
            //await for subscriber responses on in each delivery actor
            //when done, respond back here

            var pidTasks =  _subscribers.Select(s => GetPid(context, s)).ToList();
            var subscribers = await Task.WhenAll(pidTasks);
            var members = subscribers.GroupBy(subscriber => subscriber.pid.Address);

            var acks = 
                (from member in members
                        let address = member.Key
                        let subscribersOnMember = GetSubscribersForAddress(member)
                        let deliveryMessage = new DeliveryBatchMessage(subscribersOnMember, batch)
                        let deliveryPid = PID.FromAddress(address, PubSubManager.PubSubDeliveryName)
                        select context.RequestAsync<PublishResponse>(deliveryPid, deliveryMessage)).Cast<Task>()
                .ToList();
            
            await Task.WhenAll(acks);

            //ack back to producer
            context.Respond(new PublishResponse());
        }

        private static Subscribers GetSubscribersForAddress(IGrouping<string, (SubscriberIdentity subscriber, PID pid)> member) => new Subscribers()
        {
            Subscribers_ =
            {
                member.Select(s => s.subscriber).ToArray()
            }
        };

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

       

        private async Task OnClusterInit(IContext context)
        {
            _topic = context.Get<ClusterIdentity>()!.Identity;
            var subs = await LoadSubscriptions(_topic);

            if (subs?.Subscribers_ is not null)
            {
                _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
            }

            Logger.LogInformation("Topic {Topic} started", _topic);
        }

        private async Task<Subscribers> LoadSubscriptions(string topic)
        { 
            //TODO: cancellation token config?
            var state = await _subscriptionStore.GetAsync(topic, CancellationToken.None);
            Logger.LogInformation("Topic {Topic} loaded subscriptions {Subscriptions}",_topic,state);
            return state;
        }

        private async Task SaveSubscriptions(string topic, Subscribers subs)
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