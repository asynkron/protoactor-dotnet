using Google.Protobuf;
using Newtonsoft.Json;

namespace Proto.Persistence.Couchbase
{
    internal class Envelope
    {
        public string ActorName { get; }
        public int EventIndex { get; }
        public IMessage Event { get; }
        public string Type { get; }

        [JsonIgnore]
        public string Key { get; }

        public Envelope(string actorName, int eventIndex, IMessage @event, string type)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            Event = @event;
            Type = type;
            Key = $"{actorName}-event-{eventIndex}";
        }
    }
}