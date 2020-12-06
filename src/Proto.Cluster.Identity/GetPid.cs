namespace Proto.Cluster.Identity
{
    using System.Threading;
    using Router;

    internal class GetPid : IHashable
    {
        public ClusterIdentity ClusterIdentity { get; }
        public CancellationToken CancellationToken { get; }

        public GetPid(ClusterIdentity clusterIdentity, CancellationToken cancellationToken)
        {
            ClusterIdentity = clusterIdentity;
            CancellationToken = cancellationToken;
        }

        public string HashBy() => ClusterIdentity.ToShortString();
    }

    internal class PidResult
    {
        public PID? Pid { get; set; }
    }
}