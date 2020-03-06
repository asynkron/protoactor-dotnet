namespace Proto.Persistence.RavenDB
{
    class Event
    {
        public Event(string actorName, long index, object data)
        {
            ActorName = actorName;
            Index = index;
            Data = data;
            Id = $"{actorName}-event-{index}";
        }

        public string ActorName { get; }
        public long Index { get; }
        public object Data { get; }
        public string Id { get; }
    }
}
