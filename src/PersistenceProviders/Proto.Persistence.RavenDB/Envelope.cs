namespace Proto.Persistence.RavenDB
{
    public class Envelope
    {
        public Envelope(string actorName, ulong eventIndex, object @event, string type)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            Event = @event;
            Type = type;
            Id = $"{actorName}-event-{eventIndex}";
        }

        public string ActorName { get; }
        public ulong EventIndex { get; }
        public object Event { get; }
        public string Type { get; }
        public string Id { get; }
    }
}
