using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Marten;

namespace Proto.Persistence.Marten
{
    public class MartenProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public MartenProvider(IDocumentStore store) => _store = store;

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd,
            Action<object> callback)
        {
            using IDocumentSession session = _store.OpenSession();

            IReadOnlyList<Event> events = await session.Query<Event>()
                .Where(x => x.ActorName == actorName)
                .Where(x => x.Index >= indexStart && x.Index <= indexEnd)
                .OrderBy(x => x.Index)
                .ToListAsync();

            foreach (Event @event in events)
            {
                callback(@event.Data);
            }

            return events.LastOrDefault()?.Index ?? -1;
        }

        public async Task<(object Snapshot, long Index)> GetSnapshotAsync(string actorName)
        {
            using IDocumentSession session = _store.OpenSession();

            Snapshot snapshot = await session.Query<Snapshot>()
                .Where(x => x.ActorName == actorName)
                .OrderByDescending(x => x.Index)
                .FirstOrDefaultAsync();

            return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            using IDocumentSession session = _store.OpenSession();

            session.Store(new Event(actorName, index, @event));

            await session.SaveChangesAsync();

            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            using IDocumentSession session = _store.OpenSession();

            session.Store(new Snapshot(actorName, index, snapshot));

            await session.SaveChangesAsync();
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            using IDocumentSession session = _store.OpenSession();

            session.DeleteWhere<Event>(x =>
                x.ActorName == actorName &&
                x.Index <= inclusiveToIndex
            );

            await session.SaveChangesAsync();
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            using IDocumentSession session = _store.OpenSession();

            session.DeleteWhere<Snapshot>(x =>
                x.ActorName == actorName &&
                x.Index <= inclusiveToIndex
            );

            await session.SaveChangesAsync();
        }
    }
}
