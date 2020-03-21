// -----------------------------------------------------------------------
//   <copyright file="Supervision.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Immutable;
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
        IImmutableSet<PID> Children { get; }
        void EscalateFailure(Exception reason, object? message);
        void RestartChildren(Exception reason, params PID[] pids);
        void StopChildren(params PID[] pids);
        void ResumeChildren(params PID[] pids);
    }

    public static class Supervision
    {
        public static ISupervisorStrategy DefaultStrategy { get; } =
            new OneForOneStrategy((who, reason) => SupervisorDirective.Restart, 10, TimeSpan.FromSeconds(10));

        public static ISupervisorStrategy AlwaysRestartStrategy { get; } = new AlwaysRestartStrategy();
    }

    public interface ISupervisorStrategy
    {
        void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message);
    }

    public delegate SupervisorDirective Decider(PID pid, Exception reason);

    /// <summary>
    ///     AllForOneStrategy returns a new SupervisorStrategy which applies the given fault Directive from the decider to the
    ///     failing child and all its children.
    ///     This strategy is appropriate when the children have a strong dependency, such that and any single one failing would
    ///     place them all into a potentially invalid state.
    /// </summary>
    public class AllForOneStrategy : ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<AllForOneStrategy>();
        private readonly Decider _decider;
        private readonly int _maxNrOfRetries;
        private readonly TimeSpan? _withinTimeSpan;

        public AllForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _decider = decider;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
        {
            var directive = _decider(child, reason);

            switch (directive)
            {
                case SupervisorDirective.Resume:
                    LogInfo("Resuming");
                    supervisor.ResumeChildren(child);
                    break;
                case SupervisorDirective.Restart:
                    if (ShouldStop(rs))
                    {
                        LogInfo("Stopping");
                        supervisor.StopChildren(supervisor.Children.ToArray());
                    }
                    else
                    {
                        LogInfo("Restarting");
                        supervisor.RestartChildren(reason, supervisor.Children.ToArray());
                    }

                    break;
                case SupervisorDirective.Stop:
                    LogInfo("Stopping");
                    supervisor.StopChildren(supervisor.Children.ToArray());
                    break;
                case SupervisorDirective.Escalate:
                    supervisor.EscalateFailure(reason, message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            void LogInfo(string action) => Logger.LogInformation("{Action} {Actor} because of {Reason}", action,
                child.ToShortString(), reason
            );
        }

        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return true;
            }

            rs.Fail();

            if (rs.NumberOfFailures(_withinTimeSpan) > _maxNrOfRetries)
            {
                rs.Reset();
                return true;
            }

            return false;
        }
    }

    public class OneForOneStrategy : ISupervisorStrategy
    {
        private static readonly ILogger Logger = Log.CreateLogger<OneForOneStrategy>();

        private readonly Decider _decider;
        private readonly int _maxNrOfRetries;
        private readonly TimeSpan? _withinTimeSpan;

        public OneForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        {
            _decider = decider;
            _maxNrOfRetries = maxNrOfRetries;
            _withinTimeSpan = withinTimeSpan;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
        {
            var directive = _decider(child, reason);

            switch (directive)
            {
                case SupervisorDirective.Resume:
                    LogInfo("Resuming");
                    supervisor.ResumeChildren(child);
                    break;
                case SupervisorDirective.Restart:
                    if (ShouldStop(rs))
                    {
                        LogInfo("Stopping");
                        supervisor.StopChildren(child);
                    }
                    else
                    {
                        LogInfo("Restarting");
                        supervisor.RestartChildren(reason, child);
                    }

                    break;
                case SupervisorDirective.Stop:
                    LogInfo("Stopping");
                    supervisor.StopChildren(child);
                    break;
                case SupervisorDirective.Escalate:
                    supervisor.EscalateFailure(reason, message);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            void LogInfo(string action) => Logger.LogInformation("{Action} {Actor} because of {Reason}", action,
                child.ToShortString(), reason
            );
        }

        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0)
            {
                return true;
            }

            rs.Fail();

            if (rs.NumberOfFailures(_withinTimeSpan) > _maxNrOfRetries)
            {
                rs.Reset();
                return true;
            }

            return false;
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

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
        {
            if (rs.NumberOfFailures(_backoffWindow) == 0)
            {
                rs.Reset();
            }

            rs.Fail();

            var backoff = rs.FailureCount * ToNanoseconds(_initialBackoff);
            var noise = _random.Next(500);
            var duration = TimeSpan.FromMilliseconds(ToMilliseconds(backoff + noise));
            Task.Delay(duration).ContinueWith(t => supervisor.RestartChildren(reason, child));
        }

        private static long ToNanoseconds(TimeSpan timeSpan) => Convert.ToInt64(timeSpan.TotalMilliseconds * 1000000);

        private static long ToMilliseconds(long nanoseconds) => nanoseconds / 1000000;
    }

    public class AlwaysRestartStrategy : ISupervisorStrategy
    {
        //always restart
        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason,
            object? message)
            => supervisor.RestartChildren(reason, child);
    }
}