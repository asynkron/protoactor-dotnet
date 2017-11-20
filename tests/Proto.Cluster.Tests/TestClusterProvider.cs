using System.Threading.Tasks;

namespace Proto.Cluster.Tests
{
    public class TestClusterProvider : IClusterProvider
    {
        public Task RegisterMemberAsync(string clusterName, string h, int p, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer serializer)
        {
            return Task.FromResult(0);
        }

        public void MonitorMemberStatusChanges()
        {
        }

        public Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue)
        {
            return Task.FromResult(0);
        }

        public Task DeregisterMemberAsync()
        {
            return Task.FromResult(0);
        }

        public Task Shutdown()
        {
            return Task.FromResult(0);
        }
    }
}