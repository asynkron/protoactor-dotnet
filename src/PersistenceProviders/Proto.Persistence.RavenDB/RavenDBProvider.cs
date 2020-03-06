using System;
using System.Linq;
using System.Threading.Tasks;
using Raven.Client.Documents;
using Raven.Client.Documents.Indexes;
using Raven.Client.Documents.Operations;

namespace Proto.Persistence.RavenDB
{
    public class RavenDBProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public RavenDBProvider(IDocumentStore store)
        {
            _store = store;

            SetupIndexes();
        }

        private void SetupIndexes()
        {
            IndexCreation.CreateIndexes(typeof(DeleteEventIndex).Assembly, _store);
            IndexCreation.CreateIndexes(typeof(DeleteSnapshotIndex).Assembly, _store);
        }

        public async Task<long> GetEventsAsync(
            string actorName, long indexStart, long indexEnd, Action<object> callback
        )
        {
            using var session = _store.OpenAsyncSession();

            var events = await session.Query<Event>()
                .Where(x => x.ActorName == actorName)
                .Where(x => x.Index >= indexStart && x.Index <= indexEnd)
                .OrderBy(x => x.Index)
                .ToListAsync();

            foreach (var @event in events)
            {
                callback(@event.Data);
            }

            return events.LastOrDefault()?.Index ?? -1;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            using var session = _store.OpenAsyncSession();

            var snapshot = await session.Query<Snapshot>()
                .Where(x => x.ActorName == actorName)
                .OrderByDescending(x => x.Index)
                .FirstOrDefaultAsync();

            return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            using var session = _store.OpenAsyncSession();

            await session.StoreAsync(new Event(actorName, index, @event));

            await session.SaveChangesAsync();

            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            using var session = _store.OpenAsyncSession();

            await session.StoreAsync(new Snapshot(actorName, index, snapshot));

            await session.SaveChangesAsync();
        }

        public Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
            => _store.Operations.SendAsync(
                new DeleteByQueryOperation<Event>("DeleteEventIndex",
                    x => x.ActorName == actorName && x.Index <= inclusiveToIndex
                )
            );

        public Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
            => _store.Operations.SendAsync(
                new DeleteByQueryOperation<Snapshot>("DeleteSnapshotIndex",
                    x => x.ActorName == actorName && x.Index <= inclusiveToIndex
                )
            );
    }
}