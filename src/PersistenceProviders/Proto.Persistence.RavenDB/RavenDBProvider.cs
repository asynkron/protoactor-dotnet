using Raven.Client;

namespace Proto.Persistence.RavenDB
{
    public class RavenDBProvider : IProvider
    {
        private readonly IDocumentStore _store;

        public RavenDBProvider(IDocumentStore store)
        {
            _store = store;
        }

        public IEventState GetEventState()
        {
            return new RavenDBProviderState(_store);
        }

        public ISnapshotState GetSnapshotState()
        {
            return new RavenDBProviderState(_store);
        }
    }
}
