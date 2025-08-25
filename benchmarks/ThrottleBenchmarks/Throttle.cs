using System;
using System.Threading.RateLimiting;

namespace ThrottleBenchmarks;

public delegate Throttle.Valve ShouldThrottle();

public static class Throttle
{
    public enum Valve
    {
        Open,
        Closing,
        Closed
    }

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
