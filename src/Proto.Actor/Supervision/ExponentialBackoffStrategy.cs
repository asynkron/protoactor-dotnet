// -----------------------------------------------------------------------
// <copyright file="ExponentialBackoffStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
/// A supervision strategy that will try to restart the failing child while backing off exponentially.
/// </summary>
public class ExponentialBackoffStrategy : ISupervisorStrategy
{
    private readonly TimeSpan _backoffWindow;
    private readonly TimeSpan _initialBackoff;
    private readonly Random _random = new();

    /// <summary>
    /// Creates a new instance of <see cref="ExponentialBackoffStrategy"/>.
    /// </summary>
    /// <param name="backoffWindow">Maximum time for the retries</param>
    /// <param name="initialBackoff">Initial delay that will be multiplied by retry count on subsequent retries</param>
    public ExponentialBackoffStrategy(TimeSpan backoffWindow, TimeSpan initialBackoff)
    {
        _backoffWindow = backoffWindow;
        _initialBackoff = initialBackoff;
    }

    public void HandleFailure(
        ISupervisor supervisor,
        PID child,
        RestartStatistics rs,
        Exception reason,
        object? message
    )
    {
        if (rs.NumberOfFailures(_backoffWindow) == 0) rs.Reset();

        rs.Fail();

        var backoff = rs.FailureCount * (int) _initialBackoff.TotalMilliseconds;
        var noise = _random.Next(500);
        var duration = TimeSpan.FromMilliseconds(backoff + noise);
        Task.Delay(duration).ContinueWith(t => supervisor.RestartChildren(reason, child));
    }
}