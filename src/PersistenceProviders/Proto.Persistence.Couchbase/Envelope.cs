// -----------------------------------------------------------------------
//  <copyright file="Envelope.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using Google.Protobuf;
using Newtonsoft.Json;

namespace Proto.Persistence.Couchbase
{
    internal class Envelope
    {
        public Envelope(string actorName, int eventIndex, IMessage @event, string type)
        {
            ActorName = actorName;
            EventIndex = eventIndex;
            Event = @event;
            Type = type;
            Key = $"{actorName}-event-{eventIndex}";
        }

        public string ActorName { get; }
        public int EventIndex { get; }
        public IMessage Event { get; }
        public string Type { get; }

        [JsonIgnore]
        public string Key { get; }
    }
}