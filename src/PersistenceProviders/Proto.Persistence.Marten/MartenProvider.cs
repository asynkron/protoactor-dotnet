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

        public IProviderState GetState()
        {
            return new MartenProviderState(_store);
        }
    }
}
