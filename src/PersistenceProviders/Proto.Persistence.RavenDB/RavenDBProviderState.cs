using Raven.Client;
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
        }

        public async Task GetEventsAsync(string actorName, ulong eventIndexStart, Action<object> callback)
        {
            using (var session = _store.OpenAsyncSession())
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

        public async Task<Tuple<object, ulong>> GetSnapshotAsync(string actorName)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var envelope = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Type == "snapshot")
                    .OrderByDescending(x => x.EventIndex)
                    .FirstOrDefaultAsync();

                return envelope != null ? Tuple.Create((object)envelope.Event, envelope.EventIndex) : null;
            }
        }
        
        public async Task PersistEventAsync(string actorName, ulong eventIndex, object @event)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var envelope = new Envelope(actorName, eventIndex, @event, "event");

                await session.StoreAsync(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task PersistSnapshotAsync(string actorName, ulong eventIndex, object snapshot)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var envelope = new Envelope(actorName, eventIndex, snapshot, "snapshot");

                await session.StoreAsync(envelope);

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteEventsAsync(string actorName, ulong fromEventIndex)
        {
            using (var session = _store.OpenAsyncSession())
            {
                var envelopes = await session.Query<Envelope>()
                    .Where(x => x.ActorName == actorName)
                    .Where(x => x.Type == "event")
                    .Where(x => x.EventIndex <= fromEventIndex)
                    .ToListAsync();

                foreach(var envelope in envelopes)
                {
                    session.Delete(envelope.Id);
                }

                await session.SaveChangesAsync();
            }
        }

        public async Task DeleteSnapshotsAsync(string actorName, ulong fromEventIndex)
        {
            using (var session = _store.OpenAsyncSession())
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
