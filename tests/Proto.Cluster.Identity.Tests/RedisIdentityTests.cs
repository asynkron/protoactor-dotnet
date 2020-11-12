// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global
namespace Proto.Cluster.Identity.Tests
{
    using IdentityLookup;
    using Proto.Cluster.Tests;
    using Redis;
    using StackExchange.Redis;

    public class RedisIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public RedisIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
            var identity = new IdentityStorageLookup(new RedisIdentityStorage(clusterName, multiplexer));
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
    //
    // public class RedisStorageTests : IdentityStorageTests
    // {
    //     public RedisStorageTests() : base(Init)
    //     {
    //     }
    //
    //     private static IIdentityStorage Init(string clusterName)
    //     {
    //         return new RedisIdentityStorage(clusterName, ConnectionMultiplexer.Connect("localhost:6379"));
    //     }
    // }
}