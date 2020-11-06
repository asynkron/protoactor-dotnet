namespace Proto.Cluster
{
    public class ClusterInit
    {
        public ClusterInit(ClusterIdentity clusterIdentity, Cluster cluster)
        {
            ClusterIdentity = clusterIdentity;
            Cluster = cluster;
        }

        public ClusterIdentity ClusterIdentity { get; }

        public string Identity => ClusterIdentity.Identity;
        public string Kind => ClusterIdentity.Kind;

        public Cluster Cluster { get; }
    }
}