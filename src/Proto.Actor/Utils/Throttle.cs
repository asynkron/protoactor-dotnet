// -----------------------------------------------------------------------
// <copyright file="Throttle.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

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
    ///     Creates a new throttle with the given window and rate. After first event is recorded, a timer starts to reset the
    ///     number of events back to 0.
    ///     If the number of events in the meantime exceeds the limit, the valve will be closed.
    ///     This has no guarantees that the throttle opens exactly after the period, since it is reset asynchronously
    ///     Throughput has been prioritized over exact re-opening
    /// </summary>
    /// <param name="maxEventsInPeriod">Event limit</param>
    /// <param name="period">Time window to verify event limit</param>
    /// <param name="throttledCallBack">This will be called with the number of events that was throttled after the period</param>
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

        var currentEvents = 0;

        return () =>
        {
            var tries = Interlocked.Increment(ref currentEvents);

            if (tries == 1)
            {
                StartTimer(throttledCallBack);
            }

            if (tries == maxEventsInPeriod)
            {
                return Valve.Closing;
            }

            return tries > maxEventsInPeriod ? Valve.Closed : Valve.Open;
        };

        void StartTimer(Action<int>? callBack) =>
            _ = SafeTask.Run(async () =>
                {
                    await Task.Delay(period).ConfigureAwait(false);
                    var timesCalled = Interlocked.Exchange(ref currentEvents, 0);

                    if (timesCalled > maxEventsInPeriod)
                    {
                        callBack?.Invoke(timesCalled - maxEventsInPeriod);
                    }
                }
            );
    }

    public static ShouldThrottle Create(
        this ThrottleOptions options,
        Action<int>? throttledCallBack = null
    ) =>
        Create(options.MaxEventsInPeriod, options.Period, throttledCallBack);

    public static bool IsOpen(this Valve valve) => valve != Valve.Closed;
}

/// <summary>
///     Throttling options
/// </summary>
/// <param name="MaxEventsInPeriod">Max events in a period</param>
/// <param name="Period">Period to check the threshold in</param>
public record ThrottleOptions(int MaxEventsInPeriod, TimeSpan Period);