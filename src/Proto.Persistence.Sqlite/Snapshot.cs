namespace Proto.Persistence.Sqlite
{
    public class Snapshot
    {
        public Snapshot() { }

        public Snapshot(string actorName, long snapshotIndex, string snapshotData)
        {
            ActorName = actorName;
            SnapshotIndex = snapshotIndex;
            SnapshotData = snapshotData;
            Id = $"{actorName}-snapshot-{snapshotIndex}";
        }

        public string ActorName { get; set; }
        public long SnapshotIndex { get; set; }
        public string SnapshotData { get; set; }
        public string Id { get; set; }
    }
}
