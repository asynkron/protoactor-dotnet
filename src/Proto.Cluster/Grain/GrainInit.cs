namespace Proto.Cluster
{
    public class GrainInit
    {
        public GrainInit(string identity, string kind)
        {
            Identity = identity;
            Kind = kind;
        }

        public string Identity { get; }
        public string Kind { get; }
    }
}