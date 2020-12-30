using System;
using System.Linq;
using System.Threading.Tasks;
using Marten;

namespace Proto.Persistence.Marten
{
    public class MartenProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public MartenProvider(IDocumentStore store) => _store = store;

        public async Task<long> GetEventsAsync(string actorName, long indexStart, long indexEnd, Action<object> callback)
        {
            using var session = _store.OpenSession();

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
            using var session = _store.OpenSession();

            var snapshot = await session.Query<Snapshot>()
                .Where(x => x.ActorName == actorName)
                .OrderByDescending(x => x.Index)
                .FirstOrDefaultAsync();

            return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
        }

        public async Task<long> PersistEventAsync(string actorName, long index, object @event)
        {
            using var session = _store.OpenSession();

            session.Store(new Event(actorName, index, @event));

            await session.SaveChangesAsync();

            return index++;
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            using var session = _store.OpenSession();

            session.Store(new Snapshot(actorName, index, snapshot));

            await session.SaveChangesAsync();
        }

        public async Task DeleteEventsAsync(string actorName, long inclusiveToIndex)
        {
            using var session = _store.OpenSession();

            session.DeleteWhere<Event>(x =>
                x.ActorName == actorName &&
                x.Index <= inclusiveToIndex);

            await session.SaveChangesAsync();
        }

        public async Task DeleteSnapshotsAsync(string actorName, long inclusiveToIndex)
        {
            using var session = _store.OpenSession();

            session.DeleteWhere<Snapshot>(x =>
                x.ActorName == actorName &&
                x.Index <= inclusiveToIndex);

            await session.SaveChangesAsync();
        }
    }
}
