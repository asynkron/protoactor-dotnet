using Proto.Cluster.Consul;

namespace Proto.Cluster.Tests
{
    // ReSharper disable once UnusedType.Global
    public class ConsulClusterFixture : ClusterFixture
    {
        public ConsulClusterFixture() : base(3)
        {
        }

        protected override IClusterProvider GetClusterProvider() => new ConsulProvider(new ConsulProviderConfig());
    }

    // // ReSharper disable once UnusedType.Global
    // public class ConsulClusterTests : ClusterTests, IClassFixture<ConsulClusterFixture>
    // {
    //     // ReSharper disable once SuggestBaseTypeForParameter
    //     public ConsulClusterTests(ITestOutputHelper testOutputHelper, ConsulClusterFixture clusterFixture) : base(
    //         testOutputHelper, clusterFixture
    //     )
    //     {
    //     }
    // }
}