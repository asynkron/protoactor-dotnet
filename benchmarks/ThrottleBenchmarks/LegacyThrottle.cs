using System;
using System.Threading;
using System.Threading.Tasks;

namespace ThrottleBenchmarks.Legacy;

public static class LegacyThrottle
{
    public static ShouldThrottle Create(
        int maxEventsInPeriod,
        TimeSpan period,
        Action<int>? throttledCallBack = null
    )
    {
        if (maxEventsInPeriod == 0)
        {
            return () => Throttle.Valve.Closed;
        }

        if (period == TimeSpan.Zero || maxEventsInPeriod < 1 || maxEventsInPeriod == int.MaxValue)
        {
            return () => Throttle.Valve.Open;
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
                return Throttle.Valve.Closing;
            }

            return tries > maxEventsInPeriod ? Throttle.Valve.Closed : Throttle.Valve.Open;
        };

        void StartTimer(Action<int>? callBack) =>
            _ = Task.Run(async () =>
            {
                await Task.Delay(period).ConfigureAwait(false);
                var timesCalled = Interlocked.Exchange(ref currentEvents, 0);

                if (timesCalled > maxEventsInPeriod)
                {
                    callBack?.Invoke(timesCalled - maxEventsInPeriod);
                }
            });
    }
}
