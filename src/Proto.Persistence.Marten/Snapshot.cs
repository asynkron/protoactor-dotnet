namespace Proto.Persistence.Marten
{
    class Snapshot
    {
        public Snapshot(string actorName, long index, object data)
        {
            ActorName = actorName;
            Index = index;
            Data = data;
            Id = $"{actorName}-snapshot-{index}";
        }

        public string ActorName { get; }
        public long Index { get; }
        public object Data { get; }
        public string Id { get; }
    }
}
