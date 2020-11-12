namespace Proto.Cluster
{
    public static class Extensions
    {
        public static Cluster Cluster(this ActorSystem system) => system.Extensions.Get<Cluster>();
        
        public static Cluster Cluster(this IContext context) => context.System.Extensions.Get<Cluster>();
    }
}