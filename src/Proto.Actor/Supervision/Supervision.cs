// -----------------------------------------------------------------------
// <copyright file="Supervision.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

// ReSharper disable once CheckNamespace
namespace Proto;

public static class Supervision
{
    /// <summary>
    ///     Default supervision strategy is <see cref="OneForOneStrategy" />
    /// </summary>
    public static ISupervisorStrategy DefaultStrategy { get; } =
        new OneForOneStrategy((who, reason) => SupervisorDirective.Restart, 10, TimeSpan.FromSeconds(10));

    /// <summary>
    ///     Restarts the actor regardless of the failure reason
    /// </summary>
    public static ISupervisorStrategy AlwaysRestartStrategy { get; } = new AlwaysRestartStrategy();
}

/// <summary>
///     Decides how to handle the failure
/// </summary>
/// <param name="pid"><see cref="PID" /> of the actor that failed</param>
/// <param name="reason">Exception thrown by the failing actor</param>
public delegate SupervisorDirective Decider(PID pid, Exception reason);