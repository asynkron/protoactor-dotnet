﻿// -----------------------------------------------------------------------
// <copyright file = "Publisher.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub;

public class Publisher : IPublisher
{
    private readonly Cluster _cluster;

    public Publisher(Cluster cluster) => _cluster = cluster;

    /// <summary>
    /// Publishes a batch of messages to PubSub topic. For high throughput scenarios consider using <see cref="Proto.Cluster.PubSub.BatchingProducer"></see>.
    /// </summary>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="batch">Message batch</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task<PublishResponse?> PublishBatch(
        string topic,
        PubSubBatch batch,
        CancellationToken ct = default
    ) => _cluster.RequestAsync<PublishResponse>(topic, TopicActor.Kind, batch, ct, _cluster.Config.PubSubConfig.PublishTimeout);
}

public static class PublisherExtensions
{
    /// <summary>
    /// Publishes a batch of messages to PubSub topic. For high throughput scenarios consider using <see cref="Proto.Cluster.PubSub.BatchingProducer"></see>.
    /// </summary>
    /// <param name="publisher"></param>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="messages">Message</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<PublishResponse?> PublishBatch<TMessage>(
        this IPublisher publisher,
        string topic,
        IEnumerable<TMessage> messages,
        CancellationToken ct = default
    )
    {
        var batch = new PubSubBatch();
        batch.Envelopes.AddRange(messages.Cast<object>());
        return publisher.PublishBatch(topic, batch, ct);
    }

    /// <summary>
    /// Publishes a message to PubSub topic. For high throughput scenarios consider using <see cref="Proto.Cluster.PubSub.BatchingProducer"></see>.
    /// </summary>
    /// <param name="publisher"></param>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="message">Message</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public static Task<PublishResponse?> Publish(
        this IPublisher publisher,
        string topic,
        object message,
        CancellationToken ct = default
    )
    {
        var batch = new PubSubBatch {Envelopes = {message}};
        return publisher.PublishBatch(topic, batch, ct);
    }
}