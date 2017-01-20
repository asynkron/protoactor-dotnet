namespace Proto.Persistence
{
    public interface IProvider
    {
        IProviderState GetState();
    }
}