// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster.PubSub;

[PublicAPI]
public static class PubSubExtensions
{
    public static Producer Producer(this Cluster cluster, string topic) => new(cluster, topic);

    // Subscribe Cluster Identity
    public static async Task Subscribe(
        this Cluster cluster,
        string topic,
        string subscriberIdentity,
        string subscriberKind,
        CancellationToken ct = default
    )
    {
        await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ClusterIdentity.Create(subscriberIdentity, subscriberKind)
                }
            }, ct
        );
    }

    public static async Task Subscribe(this Cluster cluster, string topic, ClusterIdentity ci, CancellationToken ct = default)
    {
        await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ci
                }
            }, ct
        );
    }

    //Subscribe PID
    public static Task Subscribe(this Cluster cluster, string topic, PID subscriber, CancellationToken ct = default) =>
        cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, ct
        );

    //Subscribe Receive function, ad-hoc actor
    public static async Task<PID> Subscribe(this Cluster cluster, string topic, Receive receive)
    {
        var props = Props.FromFunc(receive);
        var pid = cluster.System.Root.Spawn(props);
        await cluster.Subscribe(topic, pid);
        return pid;
    }

    public static async Task Unsubscribe(this Cluster cluster, string topic, PID subscriber, CancellationToken ct = default)
    {
        await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, ct
        );
    }

    public static async Task Unsubscribe(this Cluster cluster, string topic, ClusterIdentity subscriber, CancellationToken ct = default)
    {
        await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = subscriber
                }
            }, ct
        );
    }

    public static async Task Unsubscribe(
        this Cluster cluster,
        string topic,
        string subscriberIdentity,
        string subscriberKind,
        CancellationToken ct = default
    )
    {
        await cluster.RequestAsync<SubscribeResponse>(topic, "topic", new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ClusterIdentity.Create(subscriberIdentity, subscriberKind)
                }
            }, ct
        );
    }
}