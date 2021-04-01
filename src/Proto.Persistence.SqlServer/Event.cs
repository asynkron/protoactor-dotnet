namespace Proto.Persistence.SqlServer
{
    internal class Event
    {
        public Event(string actorName, long eventIndex, object eventData)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            EventData = eventData;
            Id = $"{actorName}-event-{eventIndex}";
        }

        public string ActorName { get; }
        public long EventIndex { get; }
        public object EventData { get; }
        public string Id { get; }
    }
}
