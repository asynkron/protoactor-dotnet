// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using Proto.Cluster.Tests;
using StackExchange.Redis;
using Microsoft.Extensions.Configuration;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.IdentityLookup;
using Proto.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Identity.Tests
{
    public class RedisIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public RedisIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var connectionString = TestConfig.Configuration.GetConnectionString("Redis");
            var multiplexer = ConnectionMultiplexer.Connect(connectionString);
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