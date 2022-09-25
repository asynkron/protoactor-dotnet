// -----------------------------------------------------------------------
// <copyright file="TimeStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;

namespace Proto.Persistence.SnapshotStrategies;

/// <summary>
///     <see cref="ISnapshotStrategy" /> implementation that stores snapshots at certain time intervals. The condition to
///     store snapshot
///     is evaluated only when event is stored (there is no underlying timer).
/// </summary>
public class TimeStrategy : ISnapshotStrategy
{
    private readonly Func<DateTime> _getNow;
    private readonly TimeSpan _interval;
    private DateTime _lastTaken;

    /// <summary>
    ///     Creates a new instance of <see cref="TimeStrategy" />
    /// </summary>
    /// <param name="interval">Time between snapshot stores</param>
    /// <param name="getNow">Delegate to get current time (uses DateTime.Now by default)</param>
    public TimeStrategy(TimeSpan interval, Func<DateTime>? getNow = null)
    {
        _interval = interval;
        _getNow = getNow ?? (() => DateTime.Now);
        _lastTaken = _getNow();
    }

    public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
    {
        var now = _getNow();

        if (_lastTaken.Add(_interval) > now)
        {
            return false;
        }

        _lastTaken = now;

        return true;
    }
}