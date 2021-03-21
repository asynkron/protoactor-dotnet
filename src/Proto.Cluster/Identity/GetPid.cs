using System.Threading;
using Proto.Router;

namespace Proto.Cluster.Identity
{
    class GetPid : IHashable
    {
        public GetPid(ClusterIdentity clusterIdentity, CancellationToken cancellationToken)
        {
            ClusterIdentity = clusterIdentity;
            CancellationToken = cancellationToken;
        }

        public ClusterIdentity ClusterIdentity { get; }
        public CancellationToken CancellationToken { get; }

        public string HashBy() => ClusterIdentity.ToString();
    }

    class PidResult
    {
        public PidResult(PID? pid) => Pid = pid;

        public PID? Pid { get; }
    }
}