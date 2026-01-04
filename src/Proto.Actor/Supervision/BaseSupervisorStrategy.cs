// -----------------------------------------------------------------------
// <copyright file="BaseSupervisorStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Base class for supervisor strategies that share common retry and failure handling logic.
/// </summary>
public abstract class BaseSupervisorStrategy : ISupervisorStrategy
{
    protected readonly Decider _decider;
    protected readonly int _maxNrOfRetries;
    protected readonly TimeSpan? _withinTimeSpan;

    protected BaseSupervisorStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
    {
        _decider = decider;
        _maxNrOfRetries = maxNrOfRetries;
        _withinTimeSpan = withinTimeSpan;
    }

    /// <summary>
    ///     Gets the children to apply the directive to.
    /// </summary>
    /// <param name="failingChild">The child that failed</param>
    /// <param name="supervisor">The supervisor containing all children</param>
    /// <returns>Array of PIDs to apply the directive to</returns>
    protected abstract PID[] GetTargetChildren(PID failingChild, ISupervisor supervisor);

    /// <summary>
    ///     Logs the supervision action being taken.
    /// </summary>
    /// <param name="action">The action being performed (e.g., "Resuming", "Restarting", "Stopping")</param>
    /// <param name="child">The failing child</param>
    /// <param name="reason">The exception that caused the failure</param>
    protected abstract void LogAction(string action, PID child, Exception reason);

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
                LogAction("Resuming", child, reason);
                supervisor.ResumeChildren(child);
                break;

            case SupervisorDirective.Restart:
                if (ShouldStop(rs))
                {
                    LogAction("Stopping", child, reason);
                    supervisor.StopChildren(GetTargetChildren(child, supervisor));
                }
                else
                {
                    LogAction("Restarting", child, reason);
                    supervisor.RestartChildren(reason, GetTargetChildren(child, supervisor));
                }
                break;

            case SupervisorDirective.Stop:
                LogAction("Stopping", child, reason);
                supervisor.StopChildren(GetTargetChildren(child, supervisor));
                break;

            case SupervisorDirective.Escalate:
                supervisor.EscalateFailure(reason, message);
                break;

            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    protected bool ShouldStop(RestartStatistics rs)
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
