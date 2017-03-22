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
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEnvelopeEventIndex).Assembly(), _store);
            await IndexCreation.CreateIndexesAsync(typeof(DeleteEnvelopeSnapshotIndex).Assembly(), _store);
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
            var indexName = "DeleteEnvelopeEventIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Type:event AND EventIndex:[Lx0 TO Lx{fromEventIndex}]" };

            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
        }

        public async Task DeleteSnapshotsAsync(string actorName, ulong fromEventIndex)
        {
            var indexName = "DeleteEnvelopeSnapshotIndex";

            var indexQuery = new IndexQuery { Query = $"ActorName:{actorName} AND Type:snapshot AND EventIndex:[Lx0 TO Lx{fromEventIndex}]" };

            Operation operation = await _store.AsyncDatabaseCommands.DeleteByIndexAsync(indexName, indexQuery);
        }
    }
}
