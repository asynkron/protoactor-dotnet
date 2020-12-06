namespace Proto.Remote
{
    public static class ActorSystemExtensions
    {
        public static Serialization Serialization(this ActorSystem system)
        {
            return system.Extensions.Get<Serialization>();
        }
    }
}