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

        public async Task GetEventsAsync(string actorName, long eventIndexStart, Action<object> callback)
        {
            using (var session = _store.OpenSession())
            {
                var envelopes = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.EventIndex >= eventIndexStart)
                    .Where(x => x.Type == "event")
                    .OrderBy(x => x.EventIndex)
                    .ToListAsync();

                foreach (var envelope in envelopes)
                {
                    callback(envelope.Event);
                }
            }
        }

        public async Task<Tuple<object, long>> GetSnapshotAsync(string actorName)
        {
            using (var session = _store.OpenSession())
            {
                var envelope = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Type == "snapshot")
                    .OrderByDescending(x => x.EventIndex)
                    .FirstOrDefaultAsync();

                return envelope != null ? Tuple.Create((object)envelope.Event, envelope.EventIndex) : null;
            }
        }

        public async Task PersistEventAsync(string actorName, long eventIndex, object @event)
        {
            using (var session = _store.OpenSession())
            {
                var envelope = new Envelope(actorName, eventIndex, @event, "event");

                session.Store(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task PersistSnapshotAsync(string actorName, long eventIndex, object snapshot)
        {
            using (var session = _store.OpenSession())
            {
                var envelope = new Envelope(actorName, eventIndex, snapshot, "snapshot");

                session.Store(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteEventsAsync(string actorName, long fromEventIndex)
        {
            using (var session = _store.OpenSession())
            {
                var envelopes = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Type == "event")
                    .Where(x => x.EventIndex <= fromEventIndex)
                    .ToListAsync();

                foreach (var envelope in envelopes)
                {
                    session.Delete(envelope.Id);
                }

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, long fromEventIndex)
        {
            using (var session = _store.OpenSession())
            {
                var envelopes = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Type == "snapshot")
                    .Where(x => x.EventIndex <= fromEventIndex)
                    .ToListAsync();

                foreach (var envelope in envelopes)
                {
                    session.Delete(envelope.Id);
                }

                await session.SaveChangesAsync();
            }
        }
    }
}
