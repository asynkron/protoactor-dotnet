namespace Proto.Persistence.SqlServer
{
    internal class Snapshot
    {
        public Snapshot(string actorName, long snapshotIndex, object snapshotData)
        {
            ActorName = actorName;
            SnapshotIndex = snapshotIndex;
            SnapshotData = snapshotData;
            Id = $"{actorName}-snapshot-{snapshotIndex}";
        }

        public string ActorName { get; }
        public long SnapshotIndex { get; }
        public object SnapshotData { get; }
        public string Id { get; }
    }
}
