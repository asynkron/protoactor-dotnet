using System.Threading;
using Proto.Router;

namespace Proto.Cluster.MongoIdentityLookup
{
    public class GetPid : IHashable
    {
        public string Key { get; set; }
        public ClusterIdentity ClusterIdentity { get; set; }
        public string Kind { get; set; }
        public CancellationToken CancellationToken { get; set; }
        public string HashBy() => Key;
    }

    public class PidResult
    {
        public PID Pid { get; set; }
    }
}