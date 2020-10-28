namespace Proto.Utils
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;

    public delegate Throttle.Valve ShouldThrottle();

    public static class Throttle
    {
        public static ShouldThrottle Create(int maxEventsInPeriod, TimeSpan period)
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
                    StartTimer();
                }
                else if (tries == maxEventsInPeriod)
                {
                    return Valve.Closing;
                }

                return tries > maxEventsInPeriod ? Valve.Closed : Valve.Open;
            };

            void StartTimer()
            {
                Task.Run(async () =>
                    {
                        await Task.Delay(period);
                        Interlocked.Exchange(ref currentEvents, 0);
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