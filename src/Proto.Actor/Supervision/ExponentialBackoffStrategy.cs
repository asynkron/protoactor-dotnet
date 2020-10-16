using System;
using System.Threading.Tasks;
// ReSharper disable once CheckNamespace
namespace Proto
{
    public class ExponentialBackoffStrategy : ISupervisorStrategy
    {
        private readonly TimeSpan _backoffWindow;
        private readonly TimeSpan _initialBackoff;
        private readonly Random _random = new Random();

        public ExponentialBackoffStrategy(TimeSpan backoffWindow, TimeSpan initialBackoff)
        {
            _backoffWindow = backoffWindow;
            _initialBackoff = initialBackoff;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
        {
            if (rs.NumberOfFailures(_backoffWindow) == 0)
            {
                rs.Reset();
            }

            rs.Fail();

            var backoff = rs.FailureCount * (int)_initialBackoff.TotalMilliseconds;
            var noise = _random.Next(500);
            var duration = TimeSpan.FromMilliseconds(backoff + noise);
            Task.Delay(duration).ContinueWith(t => supervisor.RestartChildren(reason, child));
        }
    }
}