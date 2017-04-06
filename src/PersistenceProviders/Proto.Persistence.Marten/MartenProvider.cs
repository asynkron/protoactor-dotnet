using Marten;

namespace Proto.Persistence.Marten
{
    public class MartenProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public MartenProvider(IDocumentStore store)
        {
            _store = store;
        }

        public IEventState GetEventState()
        {
            return new MartenProviderState(_store);
        }

        public ISnapshotState GetSnapshotState()
        {
            return new MartenProviderState(_store);
        }
    }
}
