// -----------------------------------------------------------------------
// <copyright file="ISupervisorStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     Handles failures of children actors.
/// </summary>
public interface ISupervisorStrategy
{
    /// <summary>
    ///     Handle the failure
    /// </summary>
    /// <param name="supervisor">Supervisor of the children</param>
    /// <param name="child">The failing child's <see cref="PID" /></param>
    /// <param name="rs">Restart statistics</param>
    /// <param name="cause">Exception thrown by the child</param>
    /// <param name="message">Message being processed at the time of the failure</param>
    void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message);
}