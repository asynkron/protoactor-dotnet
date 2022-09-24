// -----------------------------------------------------------------------
// <copyright file="OpenTelemetryMetricsExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using OpenTelemetry.Metrics;
using Proto.Metrics;

namespace Proto.OpenTelemetry;

public static class OpenTelemetryMetricsExtensions
{
    /// <summary>
    ///     Histogram buckets definition for request-like operations
    /// </summary>
    public static readonly double[] RequestLikeHistogramBoundaries =
        { .002, .005, .01, .025, .05, .075, .1, .25, .5, .75, 1, 2.5, 5, 7.5, 10, 20, 30 };

    /// <summary>
    ///     Histogram buckets definition for queue length
    /// </summary>
    public static readonly double[] QueueLengthHistogramBoundaries =
        { 0, 1, 2, 5, 10, 20, 50, 100, 200, 500, 1000 };

    /// <summary>
    ///     Adds Proto.Actor metrics to the <see cref="MeterProviderBuilder" />
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="useRecommendedHistogramBoundaries">
    ///     If true, views will be added for histogram metrics to specify
    ///     recommended histogram boundaries.
    /// </param>
    /// <returns></returns>
    public static MeterProviderBuilder AddProtoActorInstrumentation(this MeterProviderBuilder builder,
        bool useRecommendedHistogramBoundaries = true)
    {
        builder = builder.AddMeter(ProtoMetrics.MeterName);

        if (useRecommendedHistogramBoundaries)
        {
            builder.AddView("protoactor_actor_mailbox_length",
                new ExplicitBucketHistogramConfiguration { Boundaries = QueueLengthHistogramBoundaries }
            );

            builder.AddView("protoactor_actor_messagereceive_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protoactor_threadpool_latency_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_virtualactor_spawn_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_virtualactor_requestasync_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_resolve_pid_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_identity_wait_for_activation_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_identity_get_with_global_lock_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protocluster_identity_try_acquire_lock_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );

            builder.AddView("protoremote_write_duration",
                new ExplicitBucketHistogramConfiguration { Boundaries = RequestLikeHistogramBoundaries }
            );
        }

        return builder;
    }
}