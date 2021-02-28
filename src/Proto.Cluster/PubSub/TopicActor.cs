// -----------------------------------------------------------------------
// <copyright file="TopicActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub
{
    public class TopicActor : IActor
    {
        private ImmutableHashSet<SubscriberIdentity> _subscribers = ImmutableHashSet<SubscriberIdentity>.Empty;
        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            Subscribe sub     => OnSubscribe(context, sub),
            Unsubscribe unsub => OnUnsubscribe(context, unsub),
            _                 => Task.CompletedTask,
        };

        private Task OnUnsubscribe(IContext context, Unsubscribe unsub)
        {
            _subscribers = _subscribers.Remove(unsub.Subscriber);
            return Task.CompletedTask;
        }

        private Task OnSubscribe(IContext context, Subscribe sub)
        {
            _subscribers = _subscribers.Add(sub.Subscriber);
            return Task.CompletedTask;
        }
    }
}