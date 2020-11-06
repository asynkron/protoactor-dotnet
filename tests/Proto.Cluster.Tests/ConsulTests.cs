namespace Proto.Cluster.Tests
{
    using Consul;

    // ReSharper disable once UnusedType.Global
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