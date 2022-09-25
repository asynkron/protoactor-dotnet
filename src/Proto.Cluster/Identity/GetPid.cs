using System.Threading;
using Proto.Router;

namespace Proto.Cluster.Identity;

internal class GetPid : IHashable
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

internal class PidResult
{
    public static readonly PidResult Blocked = new(true);

    public PidResult(PID? pid)
    {
        Pid = pid;
    }

    private PidResult(bool identityBlocked)
    {
        IdentityBlocked = identityBlocked;
    }

    public PID? Pid { get; }
    public bool IdentityBlocked { get; }
}