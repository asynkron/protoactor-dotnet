using System;

namespace Proto.Persistence.SnapshotStrategies
{
    public class EventTypeStrategy : ISnapshotStrategy
    {
        private readonly Type _eventType;

        public EventTypeStrategy(Type eventType)
        {
            _eventType = eventType;
        }
        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
        {
            return persistedEvent.Data.GetType() == _eventType;
        }
    }
}
