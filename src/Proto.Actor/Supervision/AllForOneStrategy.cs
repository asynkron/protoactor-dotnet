// -----------------------------------------------------------------------
// <copyright file="AllForOneStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto
{
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

        public void HandleFailure(
            ISupervisor supervisor,
            PID child,
            RestartStatistics rs,
            Exception reason,
            object? message
        )
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
                child, reason
            );
        }

        private bool ShouldStop(RestartStatistics rs)
        {
            if (_maxNrOfRetries == 0) return true;

            rs.Fail();

            if (rs.NumberOfFailures(_withinTimeSpan) > _maxNrOfRetries)
            {
                rs.Reset();
                return true;
            }

            return false;
        }
    }
}