using System.Threading.Tasks;

namespace Proto.Cluster.Tests
{
    public class TestClusterProvider : IClusterProvider
    {
        public Task RegisterMemberAsync(string clusterName, string h, int p, string[] kinds)
        {
            return Task.FromResult(0);
        }

        public void MonitorMemberStatusChanges()
        {
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