// -----------------------------------------------------------------------
// <copyright file = "PubSubConfig.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using JetBrains.Annotations;

namespace Proto.Cluster.PubSub;

[PublicAPI]
public record PubSubConfig
{
    private PubSubConfig()
    {
    }

    /// <summary>
    ///     A timeout used when delivering a message batch to a subscriber. Default is 5s.
    /// </summary>
    /// <remarks>
    ///     This value gets rounded to seconds for optimization of cancellation token creation. Note that internally,
    ///     cluster request is used to deliver messages to <see cref="ClusterIdentity" /> subscribers. This means, that the
    ///     timeout
    ///     for this request will be no less than <see cref="ClusterConfig.ActorRequestTimeout" />. Subscriber timeout defines
    ///     a total timeout of the operation while <see cref="ClusterConfig.ActorRequestTimeout" /> defines a timeout for
    ///     single retry.
    /// </remarks>
    public TimeSpan SubscriberTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    ///     A timeout used when delivering a message batch to a subscriber. Default is 5s.
    /// </summary>
    public PubSubConfig WithSubscriberTimeout(TimeSpan timeout) => this with { SubscriberTimeout = timeout };

    /// <summary>
    ///     Creates a new instance of <see cref="PubSubConfig" />
    /// </summary>
    /// <returns></returns>
    public static PubSubConfig Setup() => new();
}