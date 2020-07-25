using System.Threading.Tasks;

namespace Proto.Cluster.Tests
{
    public class TestClusterProvider : IClusterProvider
    {
        public Task StartAsync(Cluster cluster, string clusterName, string h, int p, string[] kinds,
            IMemberStatusValue? statusValue, IMemberStatusValueSerializer serializer, MemberList memberList)
        {
            return Task.FromResult(0);
        }

        public void MonitorMemberStatusChanges(Cluster cluster)
        {
        }

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue)
        {
            return Task.FromResult(0);
        }

        public Task DeregisterMemberAsync(Cluster cluster)
        {
            return Task.FromResult(0);
        }

        public Task ShutdownAsync(Cluster cluster)
        {
            return Task.FromResult(0);
        }
    }
}