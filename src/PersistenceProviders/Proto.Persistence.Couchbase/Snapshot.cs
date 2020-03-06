using Newtonsoft.Json;

namespace Proto.Persistence.Couchbase
{
    class Snapshot
    {
        public Snapshot(string actorName, long snapshotIndex, object data)
        {
            ActorName = actorName;
            SnapshotIndex = snapshotIndex;
            Data = data;
            Type = "snapshot";
            Key = $"{actorName}-snapshot-{snapshotIndex}";
        }

        public string ActorName { get; }
        public long SnapshotIndex { get; }
        public object Data { get; }
        public string Type { get; }

        [JsonIgnore]
        public string Key { get; }
    }
}
