using Proto.Extensions;

namespace Proto.Cluster
{
    public class ClusterPlugin : IActorSystemExtension
    {
        public Cluster Cluster { get; }

        public ClusterPlugin(Cluster cluster)
        {
            Cluster = cluster;
        }
    }

    public static class ClusterPluginExtensions
    {
        private static readonly ActorSystemExtensionId<ClusterPlugin> ExtensionId = new();

        public static void RegisterClusterPlugin(this ActorSystem system, Cluster cluster)
        {
            var plugin = new ClusterPlugin(cluster);
            system.Extensions.RegisterExtension(ExtensionId, plugin);
        }
        
        public static Cluster Cluster(this IContext self)
        {
            var plugin = self.System.Extensions.GetExtension(ExtensionId);
            return plugin.Cluster;
        }
    } 
}