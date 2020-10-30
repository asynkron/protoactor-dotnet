using System.Threading.Tasks;
using Proto.Cluster.Tests;
using Xunit;

namespace Proto.Cluster.Consul.Tests
{
    public class ConsulClusterFixture : ClusterFixture
    {
        public ConsulClusterFixture() : base(3)
        {
        }

        protected override IClusterProvider GetClusterProvider()
        {
            return new ConsulProvider(new ConsulProviderConfig());
        }
    }
}