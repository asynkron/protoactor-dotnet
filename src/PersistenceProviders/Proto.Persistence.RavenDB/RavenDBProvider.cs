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

        public IProviderState GetState()
        {
            return new RavenDBProviderState(_store);
        }
    }
}
