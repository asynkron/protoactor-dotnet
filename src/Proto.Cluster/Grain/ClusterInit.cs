namespace Proto.Cluster
{
    public class ClusterInit
    {
        public ClusterInit(string identity, string kind, Cluster cluster)
        {
            Identity = identity;
            Kind = kind;
            Cluster = cluster;
        }

        public string Identity { get; }
        public string Kind { get; }

        public Cluster Cluster { get; }
    }
}