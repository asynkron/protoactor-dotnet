using System;
using System.Threading.Tasks;

namespace Proto
{
    public class ExponentialBackoffStrategy : ISupervisorStrategy
    {
        private readonly TimeSpan _backoffWindow;
        private readonly long _initialBackoffNs;
        private readonly Random _random = new Random();

        public ExponentialBackoffStrategy(TimeSpan backoffWindow, TimeSpan initialBackoff)
        {
            _backoffWindow = backoffWindow;
            _initialBackoffNs = TimeConvert.ToNanoseconds(initialBackoff.TotalMilliseconds);
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
        {
            if (rs.NumberOfFailures(_backoffWindow) == 0)
            {
                rs.Reset();
            }

            rs.Fail();

            var backoff = rs.FailureCount * _initialBackoffNs;
            var noise = _random.Next(500);
            var duration = TimeSpan.FromMilliseconds(TimeConvert.ToMilliseconds(backoff + noise));
            Task.Delay(duration).ContinueWith(t => supervisor.RestartChildren(reason, child));
        }
    }
}