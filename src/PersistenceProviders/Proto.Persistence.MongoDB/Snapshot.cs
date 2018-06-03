// -----------------------------------------------------------------------
//  <copyright file="Snapshot.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace Proto.Persistence.MongoDB
{
    internal class Snapshot
    {
        public Snapshot(string actorName, long snapshotIndex, object data)
        {
            ActorName = actorName;
            SnapshotIndex = snapshotIndex;
            Data = data;
            Id = $"{actorName}-snapshot-{snapshotIndex}";
        }

        [BsonElement]
        public string ActorName { get; set; }
        
        [BsonElement]
        public long SnapshotIndex { get; set; }

        [BsonElement]
        public object Data { get; set; }

        [BsonId]
        public string Id { get; set; }
    }
}
