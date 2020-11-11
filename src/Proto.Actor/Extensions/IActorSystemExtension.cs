namespace Proto.Extensions
{
    public interface IActorSystemExtension
    {
        
    }

    public interface IActorSystemExtension<T> : IActorSystemExtension where T : IActorSystemExtension
    {
        public static int Id = ActorSystemExtension.GetNextId();
    }
}