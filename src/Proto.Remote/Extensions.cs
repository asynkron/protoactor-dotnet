namespace Proto.Remote
{
    public static class ActorSystemExtensions
    {
        public static Serialization Serialization(this ActorSystem system) => system.Extensions.Get<Serialization>();
    }
}