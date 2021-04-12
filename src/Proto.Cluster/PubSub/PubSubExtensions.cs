// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Remote;

namespace Proto.Cluster.PubSub
{
    [PublicAPI]
    public static class PubSubExtensions
    {
        public static Producer Producer(this Cluster cluster,string topic) => new(cluster,topic);

        // Subscribe Cluster Identity
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

        //Subscribe PID
        public static async Task Subscribe(this Cluster cluster,  string topic, PID subscriber) => _ = await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, CancellationToken.None
        );

        //Subscribe Receive function, ad-hoc actor
        public static Task Subscribe(this Cluster cluster, string topic, Receive receive)
        {
            var props = Props.FromFunc(receive);
            var pid = cluster.System.Root.Spawn(props);
            return cluster.Subscribe(topic, pid);
        } 
    }
}