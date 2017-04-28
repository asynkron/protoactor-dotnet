namespace Proto.Persistence
{
    public interface ISnapshotStrategy
    {
        bool ShouldTakeSnapshot(PersistedEvent persistedEvent);
    }
}