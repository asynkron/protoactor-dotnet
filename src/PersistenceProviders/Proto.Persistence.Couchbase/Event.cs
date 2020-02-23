using Newtonsoft.Json;

namespace Proto.Persistence.Couchbase
{
    class Event
    {
        public Event(string actorName, long eventIndex, object data)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            Data = data;
            Type = "event";
            Key = $"{actorName}-event-{eventIndex}";
        }

        public string ActorName { get; }
        public long EventIndex { get; }
        public object Data { get; }
        public string Type { get; }

        [JsonIgnore]
        public string Key { get; }
    }
}
