// -----------------------------------------------------------------------
//  <copyright file="MongoDBProviderState.cs" company="Asynkron HB">
//      Copyright (C) 2015-2017 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Proto.Persistence.MongoDB
{
    internal class MongoDBProviderState : IProviderState
    {
        private readonly IMongoDatabase _mongoDB;

        public MongoDBProviderState(IMongoDatabase mongoDB)
        {
            _mongoDB = mongoDB;
        }

        public async Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            var sort = Builders<Event>.Sort.Ascending("eventIndex");
            var events = await EventCollection
                                .Find(e => e.ActorName == actorName && e.EventIndex >= indexStart)
                                .Sort(sort)
                                .ToListAsync();

            foreach (var @event in events)
            {
                callback(@event.Data);
            }
        }

        public async Task<(object Data, long Index)> GetSnapshotAsync(string actorName)
        {
            var sort = Builders<Snapshot>.Sort.Descending("snapshotIndex");
            var snapshot = await SnapshotCollection
                                .Find(s => s.ActorName == actorName)
                                .Sort(sort)
                                .FirstAsync();

            return snapshot != null ? (snapshot.Data, snapshot.SnapshotIndex) : (null, 0);
        }

        public async Task PersistEventAsync(string actorName, long index, object data)
        {
            var @event = new Event(actorName, index, data);

            await EventCollection.InsertOneAsync(@event);
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object data)
        {
            var snapshot = new Snapshot(actorName, index, data);

            await SnapshotCollection.InsertOneAsync(snapshot);
        }

        public async Task DeleteEventsAsync(string actorName, long fromIndex)
        {
            await EventCollection.DeleteManyAsync(e => e.ActorName == actorName && e.EventIndex <= fromIndex);
        }

        public async Task DeleteSnapshotsAsync(string actorName, long fromIndex)
        {
            await SnapshotCollection.DeleteManyAsync(s => s.ActorName == actorName && s.SnapshotIndex <= fromIndex);
        }

        private IMongoCollection<Event> EventCollection
        {
            get => _mongoDB.GetCollection<Event>("events");
        }

        private IMongoCollection<Snapshot> SnapshotCollection
        {
            get => _mongoDB.GetCollection<Snapshot>("snapshots");
        }
    }
}
