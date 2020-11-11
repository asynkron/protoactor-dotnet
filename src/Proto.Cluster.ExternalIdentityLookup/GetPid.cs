using System.Threading;
using Proto.Router;

namespace Proto.Cluster
{
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