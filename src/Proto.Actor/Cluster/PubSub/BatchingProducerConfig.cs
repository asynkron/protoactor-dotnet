// -----------------------------------------------------------------------
// <copyright file = "BatchingProducerConfig.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.PubSub;

/// <summary>
///     Decides what to do with a publishing error in <see cref="BatchingProducer" />
/// </summary>
/// <param name="retries">Number of retries (1 after initial try)</param>
/// <param name="e">Exception that was thrown</param>
/// <param name="batch">Current batch</param>
public delegate Task<PublishingErrorDecision> PublishingErrorHandler(int retries, Exception e, PubSubBatch batch);

public record BatchingProducerConfig
{
    private static readonly ILogger Logger = Log.CreateLogger<BatchingProducer>();

    private static readonly ShouldThrottle DefaultLogThrottle = Throttle.Create(3, TimeSpan.FromSeconds(10),
        droppedLogs => Logger.LogInformation("[BatchingProducer] Throttled {LogCount} logs", droppedLogs)
    );

    /// <summary>
    ///     Maximum size of the published batch. Default: 2000.
    /// </summary>
    public int BatchSize { get; init; } = 2000;

    /// <summary>
    ///     Max size of the requests waiting in queue. If value is provided, the producer will throw
    ///     <see cref="ProducerQueueFullException" /> when queue size is exceeded. If null, the queue is unbounded. Default:
    ///     null.
    /// </summary>
    public int? MaxQueueSize { get; init; }

    /// <summary>
    ///     How long to wait for the publishing to complete, in seconds. Default: 5.
    /// </summary>
    /// <remarks>Seconds granularity allows for more optimized usage of cancellation tokens</remarks>
    public int PublishTimeoutInSeconds { get; init; } = 5;

    /// <summary>
    ///     Error handler that can decide what to do with an error when publishing a batch. Default: Fail and stop the
    ///     <see cref="BatchingProducer" />
    /// </summary>
    public PublishingErrorHandler OnPublishingError { get; init; } =
        (_, _, _) => Task.FromResult(PublishingErrorDecision.FailBatchAndStop);

    /// <summary>
    ///     A throttle for logging from this producer. By default, a throttle shared between all instances of
    ///     <see cref="BatchingProducer" /> is used, that allows for 10 events in 10 seconds.
    /// </summary>
    public ShouldThrottle LogThrottle { get; init; } = DefaultLogThrottle;

    /// <summary>
    ///     Optional idle timeout which will specify to the `IPublisher` how long it should wait before invoking clean
    ///     up code to recover resources.
    /// </summary>
    public TimeSpan? PublisherIdleTimeout { get; init; }
}
