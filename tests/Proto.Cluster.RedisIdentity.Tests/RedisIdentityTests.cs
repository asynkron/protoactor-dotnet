// ReSharper disable UnusedType.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Threading;
using Microsoft.Extensions.Configuration;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.Identity.Tests;
using Proto.Cluster.Tests;
using Proto.TestFixtures;
using StackExchange.Redis;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.RedisIdentity.Tests;

public class RedisIdentityClusterFixture : BaseInMemoryClusterFixture
{
    public RedisIdentityClusterFixture() : base(3)
    {
#if NETCOREAPP3_1
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
    }

    protected override IIdentityLookup GetIdentityLookup(string clusterName)
    {
        var identity = new IdentityStorageLookup(new RedisIdentityStorage(clusterName, RedisFixture.Multiplexer));

        return identity;
    }

    public class RedisClusterTests : ClusterTests, IClassFixture<RedisIdentityClusterFixture>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public RedisClusterTests(ITestOutputHelper testOutputHelper, RedisIdentityClusterFixture clusterFixture)
            : base(testOutputHelper, clusterFixture)
        {
        }
    }
}

public class ChaosMonkeyRedisIdentityClusterFixture : BaseInMemoryClusterFixture
{
    public ChaosMonkeyRedisIdentityClusterFixture() : base(3)
    {
    }

    protected override IIdentityLookup GetIdentityLookup(string clusterName)
    {
        var identity = new IdentityStorageLookup(
            new FailureInjectionStorage(new RedisIdentityStorage(clusterName, RedisFixture.Multiplexer,
                TimeSpan.FromSeconds(10)))
        );

        return identity;
    }

    public class ResilienceRedisClusterTests : ClusterTests, IClassFixture<ChaosMonkeyRedisIdentityClusterFixture>
    {
        // ReSharper disable once SuggestBaseTypeForParameter
        public ResilienceRedisClusterTests(
            ITestOutputHelper testOutputHelper,
            ChaosMonkeyRedisIdentityClusterFixture clusterFixture
        )
            : base(testOutputHelper, clusterFixture)
        {
        }
    }
}

internal static class RedisFixture
{
    private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new(()
        => ConnectionMultiplexer.Connect(TestConfig.Configuration.GetConnectionString("Redis")!));

    static RedisFixture()
    {
        ThreadPool.SetMinThreads(250, 250);
    }

    public static ConnectionMultiplexer Multiplexer => LazyConnection.Value;
}

public class RedisStorageTests : IdentityStorageTests
{
    public RedisStorageTests(ITestOutputHelper testOutputHelper) : base(Init, testOutputHelper)
    {
    }

    private static IIdentityStorage Init(string clusterName) =>
        new RedisIdentityStorage(clusterName,
            RedisFixture.Multiplexer,
            TimeSpan.FromMilliseconds(1500)
        );
}