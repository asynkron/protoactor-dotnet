// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Remote;

namespace Proto.Cluster.PubSub
{
    public static class PubSubExtensions
    {
        public static Producer Producer(this Cluster cluster,string topic) => new(cluster,topic);

        public static async Task Subscribe(this Cluster cluster,  string topic, string subscriberIdentity, string subscriberKind) => _ = await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest()
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = new ClusterIdentity
                    {
                        Identity = subscriberIdentity,
                        Kind = subscriberKind
                    }
                }
            }, CancellationToken.None
        );

        public static async Task Subscribe(this Cluster cluster,  string topic, PID subscriber) => _ = await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, CancellationToken.None
        );
    }
}