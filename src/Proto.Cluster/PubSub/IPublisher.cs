// -----------------------------------------------------------------------
// <copyright file = "IPublisher.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.PubSub;

public interface IPublisher
{
    /// <summary>
    ///     Initializes the internal mechanisms of this <see cref="Proto.Cluster.PubSub.IPublisher"></see>
    /// </summary>
    /// <param name="config">Configuration used to initialize this publisher</param>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    public Task Initialize(PublisherConfig? config, string topic, CancellationToken ct = default);

    /// <summary>
    ///     Publishes a batch of messages to PubSub topic. For high throughput scenarios consider using
    ///     <see cref="Proto.Cluster.PubSub.BatchingProducer"></see>.
    /// </summary>
    /// <param name="topic">Topic to publish to</param>
    /// <param name="batch">Message batch</param>
    /// <param name="ct"></param>
    /// <returns></returns>
    Task<PublishResponse> PublishBatch(
        string topic,
        PubSubBatch batch,
        CancellationToken ct = default
    );
}
