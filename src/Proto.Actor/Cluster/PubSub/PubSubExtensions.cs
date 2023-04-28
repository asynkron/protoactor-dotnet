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
    public static PubSubExtension PubSub(this Cluster cluster) => cluster.System.Extensions.Get<PubSubExtension>()!;

    public static PubSubExtension? PubSub(this ActorSystem system) => system.Extensions.Get<PubSubExtension>();

    /// <summary>
    ///     Creates a new PubSub publisher that publishes messages directly to the TopicActor
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    public static IPublisher Publisher(this Cluster cluster) => new Publisher(cluster);

    /// <summary>
    ///     Create a new PubSub batching producer for specified topic, that publishes directly to the topic actor
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to produce to</param>
    /// <param name="config">Configuration</param>
    /// <returns></returns>
    public static BatchingProducer BatchingProducer(this Cluster cluster, string topic,
        BatchingProducerConfig? config = null) =>
        new(cluster.Publisher(), topic, config);

    /// <summary>
    ///     Subscribes to a PubSub topic by cluster identity
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to subscribe to</param>
    /// <param name="subscriberIdentity">Identity of the subscriber actor</param>
    /// <param name="subscriberKind">Cluster kind of the subscriber actor</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<SubscribeResponse> Subscribe(
        this Cluster cluster,
        string topic,
        string subscriberIdentity,
        string subscriberKind,
        CancellationToken ct = default
    ) =>
        cluster.RequestAsync<SubscribeResponse>(topic, TopicActor.Kind, new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ClusterIdentity.Create(subscriberIdentity, subscriberKind)
                }
            }, ct
        );

    /// <summary>
    ///     Subscribes to a PubSub topic by cluster identity
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to subscribe to</param>
    /// <param name="ci">Cluster identity to subscribe to</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<SubscribeResponse> Subscribe(this Cluster cluster, string topic, ClusterIdentity ci,
        CancellationToken ct = default) =>
        cluster.RequestAsync<SubscribeResponse>(topic, TopicActor.Kind, new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ci
                }
            }, ct
        );

    /// <summary>
    ///     Subscribes to a PubSub topic by subscriber PID
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to subscribe to</param>
    /// <param name="subscriber">Subscriber PID</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<SubscribeResponse> Subscribe(this Cluster cluster, string topic, PID subscriber,
        CancellationToken ct = default) =>
        cluster.RequestAsync<SubscribeResponse>(topic, TopicActor.Kind, new SubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, ct
        );

    /// <summary>
    ///     Subscribe to a PubSub topic by providing a Receive function, that will be used to spawn a subscriber actor
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to subscribe to</param>
    /// <param name="receive">Message processing function</param>
    /// <returns></returns>
    public static async Task<PID> Subscribe(this Cluster cluster, string topic, Receive receive)
    {
        var props = Props.FromFunc(receive);
        var pid = cluster.System.Root.Spawn(props);
        await cluster.Subscribe(topic, pid).ConfigureAwait(false);

        return pid;
    }

    /// <summary>
    ///     Unsubscribe a PubSub topic by subscriber PID
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to unsubscribe from</param>
    /// <param name="subscriber">PID to remove from subscriber list</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<UnsubscribeResponse> Unsubscribe(this Cluster cluster, string topic, PID subscriber,
        CancellationToken ct = default) =>
        cluster.RequestAsync<UnsubscribeResponse>(topic, TopicActor.Kind, new UnsubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    Pid = subscriber
                }
            }, ct
        );

    /// <summary>
    ///     Unsubscribe a PubSub topic by subscriber cluster identity
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to unsubscribe from</param>
    /// <param name="subscriber">Cluster identity to remove from subscriber list</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<UnsubscribeResponse> Unsubscribe(this Cluster cluster, string topic, ClusterIdentity subscriber,
        CancellationToken ct = default) =>
        cluster.RequestAsync<UnsubscribeResponse>(topic, TopicActor.Kind, new UnsubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = subscriber
                }
            }, ct
        );

    /// <summary>
    ///     Unsubscribe a PubSub topic by subscriber cluster identity
    /// </summary>
    /// <param name="cluster"></param>
    /// <param name="topic">Topic to unsubscribe from</param>
    /// <param name="subscriberIdentity">Subscriber identity to remove from subscriber list</param>
    /// <param name="subscriberKind">Subscriber kind to remove from subscriber list</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<UnsubscribeResponse> Unsubscribe(
        this Cluster cluster,
        string topic,
        string subscriberIdentity,
        string subscriberKind,
        CancellationToken ct = default
    ) =>
        cluster.RequestAsync<UnsubscribeResponse>(topic, TopicActor.Kind, new UnsubscribeRequest
            {
                Subscriber = new SubscriberIdentity
                {
                    ClusterIdentity = ClusterIdentity.Create(subscriberIdentity, subscriberKind)
                }
            }, ct
        );
}