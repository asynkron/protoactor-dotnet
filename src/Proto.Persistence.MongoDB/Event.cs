// -----------------------------------------------------------------------
//  <copyright file="Event.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using MongoDB.Bson.Serialization.Attributes;

namespace Proto.Persistence.MongoDB;

internal class Event
{
    public Event(string actorName, long eventIndex, object data)
    {
        ActorName = actorName;
        EventIndex = eventIndex;
        Data = data;
        Id = $"{actorName}-event-{eventIndex}";
    }

    [BsonElement] public string ActorName { get; set; }

    [BsonElement] public long EventIndex { get; set; }

    [BsonElement] public object Data { get; set; }

    [BsonId] public string Id { get; set; }
}