using Raven.Abstractions.Data;
using Raven.Abstractions.Extensions;
using Raven.Client;
using Raven.Client.Connection;
using Raven.Client.Indexes;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Proto.Persistence.RavenDB
{
    public class RavenDBProviderState : IProviderState
    {
        private readonly IDocumentStore _store;

        public RavenDBProviderState(IDocumentStore store)
        {
            _store = store;

            SetupIndexes();
        }

        private async void SetupIndexes()
        {
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEventIndex).Assembly(), _store);
            await IndexCreation.CreateIndexesAsync(typeof(DeleteSnapshotIndex).Assembly(), _store);
        }

        public async Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var events = await session.Query<Event>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Index >= indexStart)
                    .OrderBy(x => x.Index)
                    .ToListAsync();

                foreach (var @event in events)
                {
                    callback(@event.Data);
                }
            }
        }

        public async Task<(object Data, long Index)> GetSnapshotAsync(string actorName)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var snapshot = await session.Query<Snapshot>()
                    .Where(x => x.ActorName == actorName)
                    .OrderByDescending(x => x.Index)
                    .FirstOrDefaultAsync();

                return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
            }
        }

        public async Task PersistEventAsync(string actorName, long index, object data)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var @event = new Event(actorName, index, data);

                await session.StoreAsync(@event);

                await session.SaveChangesAsync();
            }
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object data)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var snapshot = new Snapshot(actorName, index, data);

                await session.StoreAsync(snapshot);

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteEventsAsync(string actorName, long fromIndex)
        {
            var indexName = "DeleteEventIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Index_Range:[Lx0 TO Lx{fromIndex}]" };

            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
        }

        public async Task DeleteSnapshotsAsync(string actorName, long fromIndex)
        {
            var indexName = "DeleteSnapshotIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Index_Range:[Lx0 TO Lx{fromIndex}]" };

            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
        }
    }
}