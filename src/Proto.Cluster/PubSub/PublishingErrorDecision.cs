// -----------------------------------------------------------------------
// <copyright file = "PublishingErrorDecision.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Cluster.PubSub;

public sealed class PublishingErrorDecision
{
    /// <summary>
    ///     Causes the <see cref="BatchingProducer" /> to stop and fail the pending messages
    /// </summary>
    public static readonly PublishingErrorDecision FailBatchAndStop = new();

    /// <summary>
    ///     Skips the current batch and proceeds to the next one. The delivery reports (tasks) related to that batch are still
    ///     failed with the exception that triggered the error handling.
    /// </summary>
    public static readonly PublishingErrorDecision FailBatchAndContinue = new();

    /// <summary>
    ///     Retries the current batch immediately
    /// </summary>
    public static readonly PublishingErrorDecision RetryBatchImmediately = new();

    private PublishingErrorDecision()
    {
    }

    internal TimeSpan? Delay { get; private set; }

    /// <summary>
    ///     Retries current batch after specified delay
    /// </summary>
    /// <param name="delay"></param>
    /// <returns></returns>
    public static PublishingErrorDecision RetryBatchAfter(TimeSpan delay) =>
        new()
        {
            Delay = delay
        };
}