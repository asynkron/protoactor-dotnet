using System;
using System.Linq;
using System.Threading.Tasks;
using Marten;

namespace Proto.Persistence.Marten
{
    public class MartenProviderState : IProviderState
    {
        private readonly IDocumentStore _store;

        public MartenProviderState(IDocumentStore store)
        {
            _store = store;
        }

        public async Task GetEventsAsync(string actorName, long indexStart, Action<object> callback)
        {
            using (var session = _store.OpenSession())
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
            using (var session = _store.OpenSession())
            {
                var snapshot = await session.Query<Snapshot>()
                    .Where(x => x.ActorName == actorName)
                    .OrderByDescending(x => x.Index)
                    .FirstOrDefaultAsync();

                return snapshot != null ? (snapshot.Data, snapshot.Index) : (null, 0);
            }
        }

        public async Task PersistEventAsync(string actorName, long index, object @event)
        {
            using (var session = _store.OpenSession())
            {
                var envelope = new Event(actorName, index, @event);

                session.Store(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task PersistSnapshotAsync(string actorName, long index, object snapshot)
        {
            using (var session = _store.OpenSession())
            {
                var envelope = new Snapshot(actorName, index, snapshot);

                session.Store(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteEventsAsync(string actorName, long fromIndex)
        {
            using (var session = _store.OpenSession())
            {
                session.DeleteWhere<Event>(x =>
                    x.ActorName == actorName &&
                    x.Index <= fromIndex);

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long fromIndex)
        {
            using (var session = _store.OpenSession())
            {
                session.DeleteWhere<Snapshot>(x =>
                    x.ActorName == actorName &&
                    x.Index <= fromIndex);

                await session.SaveChangesAsync();
            }
        }
    }
}
