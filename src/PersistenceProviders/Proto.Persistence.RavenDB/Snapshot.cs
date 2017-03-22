namespace Proto.Persistence.RavenDB
{
    internal class Snapshot
    {
        public Snapshot(string actorName, ulong index, object data)
        {
            ActorName = actorName;
            Index = index;
            Data = data;
            Id = $"{actorName}-snapshot-{index}";
        }

        public string ActorName { get; }
        public ulong Index { get; }
        public object Data { get; }
        public string Id { get; }
    }
}
