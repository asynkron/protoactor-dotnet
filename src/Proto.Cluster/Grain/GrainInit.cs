namespace Proto.Cluster
{
    public class GrainInit
    {
        public GrainInit(string identity, string kind, Cluster cluster)
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