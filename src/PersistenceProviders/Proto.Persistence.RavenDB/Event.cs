namespace Proto.Persistence.RavenDB
{
    internal class Event
    {
        public Event(string actorName, ulong index, object data)
        {
            ActorName = actorName;
            Index = index;
            Data = data;
            Id = $"{actorName}-event-{index}";
        }

        public string ActorName { get; }
        public ulong Index { get; }
        public object Data { get; }
        public string Id { get; }
    }
}
