// -----------------------------------------------------------------------
// <copyright file="Throttle.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Utils
{
    public delegate Throttle.Valve ShouldThrottle();

    public static class Throttle
    {
        public enum Valve
        {
            Open,
            Closing,
            Closed
        }

        /// <summary>
        ///     This has no guarantees that the throttle opens exactly after the period, since it is reset asynchronously
        ///     Throughput has been prioritized over exact re-opening
        /// </summary>
        /// <param name="maxEventsInPeriod"></param>
        /// <param name="period"></param>
        /// <param name="throttledCallBack">This will be called with the number of events what was throttled after the period</param>
        /// <returns></returns>
        public static ShouldThrottle Create(
            int maxEventsInPeriod,
            TimeSpan period,
            Action<int>? throttledCallBack = null
        )
        {
            if (period == TimeSpan.Zero || maxEventsInPeriod < 1 || maxEventsInPeriod == int.MaxValue)
                return () => Valve.Open;

            var currentEvents = 0;
            return () => {
                var tries = Interlocked.Increment(ref currentEvents);
                if (tries == 1) StartTimer(throttledCallBack);

                if (tries == maxEventsInPeriod) return Valve.Closing;

                return tries > maxEventsInPeriod ? Valve.Closed : Valve.Open;
            };

            void StartTimer(Action<int>? callBack) => _ = Task.Run(async () => {
                    await Task.Delay(period);
                    var timesCalled = Interlocked.Exchange(ref currentEvents, 0);
                    if (timesCalled > maxEventsInPeriod) callBack?.Invoke(timesCalled - maxEventsInPeriod);
                }
            );
        }

        public static bool IsOpen(this Valve valve) => valve != Valve.Closed;
    }
}