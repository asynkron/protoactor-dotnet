// -----------------------------------------------------------------------
//  <copyright file="MongoDBProvider.cs" company="Asynkron HB">
//      Copyright (C) 2015-2018 Asynkron HB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using MongoDB.Driver;

namespace Proto.Persistence.MongoDB
{
    public class MongoDBProvider : IProvider
    {
        private readonly IMongoDatabase _mongoDB;

        public MongoDBProvider(IMongoDatabase mongoDB)
        {
            _mongoDB = mongoDB;
            SetupIndexes();
        }

        private async void SetupIndexes()
        {
            await EventCollection.Indexes.CreateOneAsync(Builders<Event>.IndexKeys
                .Ascending(_ => _.ActorName)
                .Ascending(_ => _.EventIndex));
            await SnapshotCollection.Indexes.CreateOneAsync(Builders<Snapshot>.IndexKeys
                .Ascending(_ => _.ActorName)
                .Descending(_ => _.SnapshotIndex));
        }
        
        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            var sort = Builders<Event>.Sort.Ascending("eventIndex");
            var events = await EventCollection
                .Find(e => e.ActorName == actorName && e.EventIndex >= indexStart && e.EventIndex <= indexEnd)
                .Sort(sort)
                .ToListAsync();

            foreach (var @event in events)
            {
                callback(@event.Data);
            }
            
            return events.Any() ? events.LastOrDefault().EventIndex : -1;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            var sort = Builders<Snapshot>.Sort.Descending("snapshotIndex");
            var snapshot = await SnapshotCollection
                                .Find(s => s.ActorName == actorName)
                                .Sort(sort)
                                .FirstOrDefaultAsync();

            return snapshot != null ? (snapshot.Data, snapshot.SnapshotIndex) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            await EventCollection.InsertOneAsync(new Event(actorName, index, @event));
            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            await SnapshotCollection.InsertOneAsync(new Snapshot(actorName, index, snapshot));
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            await EventCollection.DeleteManyAsync(e => e.ActorName == actorName && e.EventIndex <= inclusiveToIndex);
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            await SnapshotCollection.DeleteManyAsync(s => s.ActorName == actorName && s.SnapshotIndex <= inclusiveToIndex);
        }

        private IMongoCollection<Event> EventCollection => _mongoDB.GetCollection<Event>("events");

        private IMongoCollection<Snapshot> SnapshotCollection => _mongoDB.GetCollection<Snapshot>("snapshots");
    }
}
