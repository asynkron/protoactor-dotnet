// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.Cluster.PubSub
{
    public class TopicActor : IActor
    {
        private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
        private string _topic = string.Empty;

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            ClusterInit ci           => OnClusterInit(context, ci),
            SubscribeRequest sub     => OnSubscribe(context, sub),
            UnsubscribeRequest unsub => OnUnsubscribe(context, unsub),
            SystemMessage            => Task.CompletedTask,
            { } pub                  => OnPublishRequest(context,pub),
        };

        private async Task OnPublishRequest(IContext context, object pub)
        {
            foreach (var s in _subscribers)
            {
                switch (s.IdentityCase)
                {
                    case SubscriberIdentity.IdentityOneofCase.None:   
                        //pass;
                        break;
                    case SubscriberIdentity.IdentityOneofCase.Pid:
                        await context.RequestAsync<PublishResponse>(s.Pid, pub);
                        break;
                    case SubscriberIdentity.IdentityOneofCase.ClusterIdentity:
                        await context.ClusterRequestAsync<PublishResponse>(s.ClusterIdentity.Identity, s.ClusterIdentity.Kind, pub,
                            CancellationToken.None
                        );
                        break;
                }
            }
            context.Respond(new PublishResponse());
        }

        private async Task OnClusterInit(IContext context, ClusterInit ci)
        {
            _topic = ci.Identity;
            var subs = await LoadSubscriptions(_topic);
            _subscribers = ImmutableHashSet.CreateRange(subs.Subscribers_);
        }

        protected virtual Task<Subscribers> LoadSubscriptions(string topic) => Task.FromResult(new Subscribers());

        protected virtual Task SaveSubscriptions(string topic, Subscribers subs)
        {
            return Task.CompletedTask;
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