// -----------------------------------------------------------------------
// <copyright file="AllForOneStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
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
public class AllForOneStrategy : BaseSupervisorStrategy
{
    private static readonly ILogger Logger = Log.CreateLogger<AllForOneStrategy>();

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
        : base(decider, maxNrOfRetries, withinTimeSpan)
    {
    }

    protected override PID[] GetTargetChildren(PID failingChild, ISupervisor supervisor)
        => supervisor.Children.ToArray();

    protected override void LogAction(string action, PID child, Exception reason)
        => Logger.AllForOneStrategyAction(action, child, reason);
}
