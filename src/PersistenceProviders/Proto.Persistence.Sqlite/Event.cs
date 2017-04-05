namespace Proto.Persistence.Sqlite
{
    public class Event
    {
        public Event() { }

        public Event(string actorName, long eventIndex, string eventData)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            EventData = eventData;
            Id = $"{actorName}-event-{eventIndex}";
        }

        public string ActorName { get; set; }
        public long EventIndex { get; set; }
        public string EventData { get; set; }
        public string Id { get; set; }
    }
}
