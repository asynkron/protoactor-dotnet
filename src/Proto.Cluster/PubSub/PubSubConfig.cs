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
    /// A timeout used when delivering a message batch to a subscriber.
    /// </summary>
    public TimeSpan SubscriberTimeout { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>
    /// A timeout used when delivering a message batch from the topic to a member that hosts a subset of subscribers. Default is 10s.
    /// </summary>
    /// <remarks>Should be more than <see cref="SubscriberTimeout"/></remarks>
    public TimeSpan MemberDeliveryTimeout { get; init; } = TimeSpan.FromSeconds(10);

    /// <summary>
    /// A default timeout used when publishing a message batch or a message to a topic. Default is 15s. 
    /// </summary>
    /// <remarks>Should be more than both <see cref="SubscriberTimeout"/> and <see cref="MemberDeliveryTimeout"/></remarks>
    public TimeSpan PublishTimeout { get; init; } = TimeSpan.FromSeconds(15);

    /// <summary>
    /// A timeout used when delivering a message batch to a subscriber.
    /// </summary>
    public PubSubConfig WithSubscriberTimeout(TimeSpan timeout) =>
        this with {SubscriberTimeout = timeout};

    /// <summary>
    /// A timeout used when delivering a message batch from the topic to a member that hosts a subset of subscribers. Default is 10s.
    /// </summary>
    /// <remarks>Should be more than <see cref="SubscriberTimeout"/></remarks>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public PubSubConfig WithMemberDeliveryTimeout(TimeSpan timeout) =>
        this with {MemberDeliveryTimeout = timeout};

    /// <summary>
    /// <summary>
    /// A default timeout used when publishing a message batch or a message to a topic. Default is 15s. 
    /// </summary>
    /// <remarks>Should be more than both <see cref="SubscriberTimeout"/> and <see cref="MemberDeliveryTimeout"/></remarks>
    /// </summary>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public PubSubConfig WithPublishTimeout(TimeSpan timeout) =>
        this with {PublishTimeout = timeout};

    /// <summary>
    /// Creates a new instance of <see cref="PubSubConfig"/>
    /// </summary>
    /// <returns></returns>
    public static PubSubConfig Setup() => new();
}