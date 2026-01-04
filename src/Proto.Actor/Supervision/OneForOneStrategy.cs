// -----------------------------------------------------------------------
// <copyright file="OneForOneStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Microsoft.Extensions.Logging;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Supervision strategy that applies the supervision directive only to the failing child.
///     See
///     <a href="https://proto.actor/docs/supervision/#one-for-one-strategy-vs-all-for-one-strategy">
///         One-For-One strategy
///         vs All-For-One strategy
///     </a>
///     This strategy is appropriate when the failing child can be restarted independently from other children of the
///     supervisor.
/// </summary>
public class OneForOneStrategy : BaseSupervisorStrategy
{
    private static readonly ILogger Logger = Log.CreateLogger<OneForOneStrategy>();

    /// <summary>
    ///     Creates a new instance of the <see cref="OneForOneStrategy" /> class.
    /// </summary>
    /// <param name="decider">
    ///     A delegate that provided with failing child <see cref="PID" /> and the exception returns a
    ///     <see cref="SupervisorDirective" />
    /// </param>
    /// <param name="maxNrOfRetries">Number of restart retries before stopping the failing child of the supervisor</param>
    /// <param name="withinTimeSpan">A time window to count <see cref="maxNrOfRetries" /> in</param>
    public OneForOneStrategy(Decider decider, int maxNrOfRetries, TimeSpan? withinTimeSpan)
        : base(decider, maxNrOfRetries, withinTimeSpan)
    {
    }

    protected override PID[] GetTargetChildren(PID failingChild, ISupervisor supervisor) => new[] { failingChild };

    protected override void LogAction(string action, PID child, Exception reason)
    {
        if (Logger.IsEnabled(LogLevel.Information))
        {
            Logger.OneForOneStrategyAction(action, child, reason);
        }
    }
}
