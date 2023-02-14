// -----------------------------------------------------------------------
// <copyright file="AllForOneStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Supervision strategy that applies the supervision directive to all the children.
///     See
///     <a href="https://proto.actor/docs/supervision/#one-for-one-strategy-vs-all-for-one-strategy">
///         One-For-One strategy
///         vs All-For-One strategy
///     </a>
///     This strategy is appropriate when the children have a strong dependency, such that and any single one failing would
///     place them all into a potentially invalid state.
/// </summary>
public partial class AllForOneStrategy : ISupervisorStrategy
{
    private static readonly (string Resume, string Restart, string Stop) Actions = ("Resuming", "Restarting", "Stopping");
    private static readonly ILogger Logger = Log.CreateLogger<AllForOneStrategy>();
    private readonly Decider _decider;
    private readonly int _maxNrOfRetries;
    private readonly TimeSpan? _withinTimeSpan;

    /// <summary>
    ///     Creates a new instance of the <see cref="AllForOneStrategy" />
    /// </summary>
    /// <param name="decider">
    ///     A delegate that provided with failing child <see cref="PID" /> and the exception returns a
    ///     <see cref="SupervisorDirective" />
    /// </param>
    /// <param name="maxNrOfRetries">Number of restart retries before stopping the the children of the supervisor</param>
    /// <param name="withinTimeSpan">A time window to count <see cref="maxNrOfRetries" /> in</param>
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
                LogAction(reason, Actions.Resume, child, reason.Message);
                supervisor.ResumeChildren(child);

                break;
            case SupervisorDirective.Restart:
                if (ShouldStop(rs))
                {
                    LogAction(reason, Actions.Stop, child, reason.Message);
                    supervisor.StopChildren(supervisor.Children.ToArray());
                }
                else
                {
                    LogAction(reason, Actions.Restart, child, reason.Message);
                    supervisor.RestartChildren(reason, supervisor.Children.ToArray());
                }

                break;
            case SupervisorDirective.Stop:
                LogAction(reason, Actions.Stop, child, reason.Message);
                supervisor.StopChildren(supervisor.Children.ToArray());

                break;
            case SupervisorDirective.Escalate:
                supervisor.EscalateFailure(reason, message);

                break;
            default:
                throw new ArgumentOutOfRangeException();
        }
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

    [LoggerMessage(0, LogLevel.Information, "{Action} {Actor} because of {Reason}")]
    partial void LogAction(Exception ex, string action, PID actor, string reason);
}