namespace Proto.Persistence
{
    public interface IPersistentActor : IActor
    {
        Persistence Persistence { get; set; }
    }
}