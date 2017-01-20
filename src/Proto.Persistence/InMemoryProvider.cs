namespace Proto.Persistence
{
    public class InMemoryProvider : IProvider
    {
        public IProviderState GetState()
        {
            return new InMemoryProviderState();
        }
    }
}