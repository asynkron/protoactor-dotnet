using Proto.Cluster.IdentityLookup;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    // ReSharper disable once UnusedType.Global
    public class RedisIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public RedisIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
            var identity = new ExternalIdentityLookup(new RedisIdentityStorage(clusterName, multiplexer));
            return identity;
        }
    }

    // public class RedisClusterTests : ClusterTests, IClassFixture<RedisIdentityClusterFixture>
    // {
    //     // ReSharper disable once SuggestBaseTypeForParameter
    //     public RedisClusterTests(ITestOutputHelper testOutputHelper, RedisIdentityClusterFixture clusterFixture)
    //         : base(testOutputHelper, clusterFixture)
    //     {
    //     }
    // }
}