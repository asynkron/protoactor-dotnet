// -----------------------------------------------------------------------
//  <copyright file="Supervision.cs" company="Asynkron HB">
//      Copyright (C) 2015-2016 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto
{
    public enum SupervisorDirective
    {
        Resume,
        Restart,
        Stop,
        Escalate
    }

    public interface ISupervisor
    {
        IReadOnlyCollection<PID> Children { get; }
        Task EscalateFailureAsync(PID who, Exception reason);
        Task RestartChildrenAsync(params PID[] pids);
        Task StopChildrenAsync(params PID[] pids);
        Task ResumeChildrenAsync(params PID[] pids);
    }

    public static class Supervision
    {
        public static ISupervisorStrategy DefaultStrategy { get; } =
            new OneForOneStrategy((who, reason) => SupervisorDirective.Restart, 10, TimeSpan.FromSeconds(10));
    }

    public interface ISupervisorStrategy
    {
        Task HandleFailureAsync(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause);
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);

    /// <summary>
    /// AllForOneStrategy returns a new SupervisorStrategy which applies the given fault Directive from the decider to the
    /// failing child and all its children.
    ///
    /// This strategy is appropriate when the children have a strong dependency, such that and any single one failing would
    /// place them all into a potentially invalid state.
    /// </summary>
    public class AllForOneStrategy : ISupervisorStrategy {
        private readonly Decider _decider;
        private readonly int _maxNrOfRetries;
        private readonly TimeSpan? _withinTimeSpan;
        private static readonly ILogger Logger = Log.CreateLogger<AllForOneStrategy>();

        public AllForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _decider = decider;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;
        }

        public async Task HandleFailureAsync(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason)
        {
            var directive = _decider(child, reason);
            switch (directive)
            {
                case SupervisorDirective.Resume:
                    Logger.LogInformation($"Resuming {child.ToShortString()} Reason {reason}");
                    await supervisor.ResumeChildrenAsync(child);
                    break;
                case SupervisorDirective.Restart:
                    if (RequestRestartPermission(rs))
                    {
                        Logger.LogInformation($"Restarting {child.ToShortString()} Reason {reason}");
                        await supervisor.RestartChildrenAsync(supervisor.Children.ToArray());
                    }
                    else
                    {
                        Logger.LogInformation($"Stopping {child.ToShortString()} Reason { reason}");
                        await supervisor.StopChildrenAsync(supervisor.Children.ToArray());
                    }
                    break;
                case SupervisorDirective.Stop:
                    Logger.LogInformation($"Stopping {child.ToShortString()} Reason {reason}");
                    await supervisor.StopChildrenAsync(supervisor.Children.ToArray());
                    break;
                case SupervisorDirective.Escalate:
                    await supervisor.EscalateFailureAsync(child, reason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool RequestRestartPermission(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return false;
            }
            rs.Fail();
            if (_withinTimeSpan == null || rs.IsWithinDuration(_withinTimeSpan.Value))
            {
                return rs.FailureCount <= _maxNrOfRetries;
            }
            rs.Reset();
            return true;
        }
    }

    public class OneForOneStrategy : ISupervisorStrategy
    {
        private readonly int _maxNrOfRetries;
        private readonly TimeSpan? _withinTimeSpan;
        private readonly Decider _decider;
        private static readonly ILogger Logger = Log.CreateLogger<OneForOneStrategy>();

        public OneForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _decider = decider;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;
        }

        public async Task HandleFailureAsync(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason)
        {
            var directive = _decider(child, reason);
            switch (directive)
            {
                case SupervisorDirective.Resume:
                    await supervisor.ResumeChildrenAsync(child);
                    break;
                case SupervisorDirective.Restart:
                    if (RequestRestartPermission(rs))
                    {
                        Logger.LogInformation($"Restarting {child.ToShortString()} Reason {reason}");
                        await supervisor.RestartChildrenAsync(child);
                    }
                    else
                    {
                        Logger.LogInformation($"Stopping {child.ToShortString()} Reason { reason}");
                        await supervisor.StopChildrenAsync(child);
                    }
                    break;
                case SupervisorDirective.Stop:
                    Logger.LogInformation($"Stopping {child.ToShortString()} Reason {reason}");
                    await supervisor.StopChildrenAsync(child);
                    break;
                case SupervisorDirective.Escalate:
                    await supervisor.EscalateFailureAsync(child, reason);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private bool RequestRestartPermission(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return false;
            }
            rs.Fail();
            if (_withinTimeSpan == null || rs.IsWithinDuration(_withinTimeSpan.Value))
            {
                return rs.FailureCount <= _maxNrOfRetries;
            }
            rs.Reset();
            return true;
        }
    }

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

        private long ToNanoseconds(TimeSpan timeSpan)
        {
            return Convert.ToInt64(timeSpan.TotalMilliseconds * 1000000);
        }

        private long ToMilliseconds(long nanoseconds)
        {
            return nanoseconds / 1000000;
        }

        public Task HandleFailureAsync(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason)
        {
            SetFailureCount(rs);
            var backoff = rs.FailureCount * ToNanoseconds(_initialBackoff);
            var noise = _random.Next(500);
            var duration = TimeSpan.FromMilliseconds(ToMilliseconds(backoff + noise));
            if (supervisor != null)
            {
                Task.Delay(duration).ContinueWith(t =>
                {
                    supervisor.RestartChildrenAsync(child);
                });
            }
            return Actor.Done;
        }

        private void SetFailureCount(RestartStatistics rs)
        {
            // if we are within the backoff window, exit early
            if (rs.IsWithinDuration(_backoffWindow))
            {
                rs.Fail();
                return;
            }
            //we are past the backoff limit, reset the failure counter
            rs.Reset();
        }
    }
}