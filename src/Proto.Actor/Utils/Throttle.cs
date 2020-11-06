namespace Proto.Utils
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate Throttle.Valve ShouldThrottle();

    public static class Throttle
    {
        /// <param name="maxEventsInPeriod"></param>
        /// <param name="period"></param>
        /// <param name="throttledCallBack">This will be called with the number of events what was throttled after the period</param>
        /// <returns></returns>
        public static ShouldThrottle Create(int maxEventsInPeriod, TimeSpan period,
            Action<int>? throttledCallBack = null)
        {
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

            void StartTimer(Action<int>? callBack)
            {
                _ = Task.Run(async () =>
                    {
                        await Task.Delay(period);
                        var timesCalled = Interlocked.Exchange(ref currentEvents, 0);
                        if (timesCalled > maxEventsInPeriod)
                        {
                            callBack?.Invoke(timesCalled - maxEventsInPeriod);
                        }
                    }
                );
            }
        }

        public enum Valve
        {
            Open,
            Closing,
            Closed
        }

        public static bool IsOpen(this Valve valve)
        {
            return valve != Valve.Closed;
        }
    }
}