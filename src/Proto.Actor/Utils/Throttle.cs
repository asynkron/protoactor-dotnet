// -----------------------------------------------------------------------
// <copyright file="Throttle.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.RateLimiting;

namespace Proto.Utils;

/// <summary>
///     Records an event when called, and returns current state of the throttle valve
/// </summary>
public delegate Throttle.Valve ShouldThrottle();

/// <summary>
///     Used for throttling events in a given time window.
/// </summary>
public static class Throttle
{
    public enum Valve
    {
        /// <summary>
        ///     Business as usual, continue processing events
        /// </summary>
        Open,

        /// <summary>
        ///     Next event will close the valve
        /// </summary>
        Closing,

        /// <summary>
        ///     Limit exceeded, stop processing events for now
        /// </summary>
        Closed
    }

    /// <summary>
    ///     Creates a new throttle with the given window and rate using a token bucket rate limiter.
    ///     Tokens are replenished automatically, removing the need for external timers.
    /// </summary>
    /// <param name="maxEventsInPeriod">Event limit</param>
    /// <param name="period">Time window to verify event limit</param>
    /// <param name="throttledCallBack">Invoked with the number of throttled events once the limiter opens again</param>
    /// <returns>
    ///     <see cref="ShouldThrottle" /> delegate that records an event when called, and returns current state of the
    ///     throttle valve
    /// </returns>
    public static ShouldThrottle Create(
        int maxEventsInPeriod,
        TimeSpan period,
        Action<int>? throttledCallBack = null
    )
    {
        if (maxEventsInPeriod == 0)
        {
            return () => Valve.Closed;
        }

        if (period == TimeSpan.Zero || maxEventsInPeriod < 1 || maxEventsInPeriod == int.MaxValue)
        {
            return () => Valve.Open;
        }

        var limiter = new TokenBucketRateLimiter(new TokenBucketRateLimiterOptions
        {
            TokenLimit = maxEventsInPeriod,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0,
            ReplenishmentPeriod = period,
            TokensPerPeriod = maxEventsInPeriod,
            AutoReplenishment = true
        });

        var dropped = 0;

        return () =>
        {
            var lease = limiter.AttemptAcquire(1);

            if (lease.IsAcquired)
            {
                if (dropped > 0)
                {
                    throttledCallBack?.Invoke(dropped);
                    dropped = 0;
                }

                var stats = limiter.GetStatistics();
                return stats.CurrentAvailablePermits == 0 ? Valve.Closing : Valve.Open;
            }

            dropped++;
            return Valve.Closed;
        };
    }

    public static bool IsOpen(this Valve valve) => valve != Valve.Closed;
}
