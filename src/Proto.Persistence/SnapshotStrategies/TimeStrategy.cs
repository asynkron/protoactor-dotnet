// -----------------------------------------------------------------------
// <copyright file="TimeStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Persistence.SnapshotStrategies
{
    public class TimeStrategy : ISnapshotStrategy
    {
        private readonly Func<DateTime> _getNow;
        private readonly TimeSpan _interval;
        private DateTime _lastTaken;

        public TimeStrategy(TimeSpan interval, Func<DateTime>? getNow = null)
        {
            _interval = interval;
            _getNow = getNow ?? (() => DateTime.Now);
            _lastTaken = _getNow();
        }

        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
        {
            var now = _getNow();
            if (_lastTaken.Add(_interval) > now) return false;

            _lastTaken = now;
            return true;
        }
    }
}