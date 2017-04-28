namespace Proto.Persistence.SnapshotStrategies
{
    public class IntervalStrategy : ISnapshotStrategy
    {
        private readonly int _eventsPerSnapshot;

        public IntervalStrategy(int eventsPerSnapshot)
        {
            _eventsPerSnapshot = eventsPerSnapshot;
        }

        public bool ShouldTakeSnapshot(PersistedEvent persistedEvent)
        {
            return persistedEvent.Index % _eventsPerSnapshot == 0;
        }
    }
}