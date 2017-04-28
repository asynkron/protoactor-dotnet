using System;

namespace Proto.Persistence.SnapshotStrategies
{
    public class TimeStrategy : ISnapshotStrategy
    {
        private readonly TimeSpan _interval;
        private readonly Func<DateTime> _getNow;
        private DateTime _lastTaken;

        public TimeStrategy(TimeSpan interval, Func<DateTime> getNow = null)
        {
            _interval = interval;
            _getNow = getNow ?? (() => DateTime.Now);
            _lastTaken = _getNow();
        }

        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
        {
            var now = _getNow();
            if (_lastTaken.Add(_interval) <= now)
            {
                _lastTaken = now;
                return true;
            }
            return false;
        }
    }
}