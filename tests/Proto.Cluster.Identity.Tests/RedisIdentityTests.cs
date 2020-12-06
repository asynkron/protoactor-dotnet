// // ReSharper disable UnusedType.Global
// // ReSharper disable UnusedMember.Global
//
// using System;
// using System.Threading;
// using Microsoft.Extensions.Configuration;
// using Proto.Cluster.Identity.Redis;
// using Proto.Cluster.IdentityLookup;
// using Proto.Cluster.Tests;
// using Proto.TestFixtures;
// using StackExchange.Redis;
// using Xunit;
// using Xunit.Abstractions;
//
// namespace Proto.Cluster.Identity.Tests
// {
//     public class RedisIdentityClusterFixture : BaseInMemoryClusterFixture
//     {
//         public RedisIdentityClusterFixture() : base(3)
//         {
//         }
//
//         protected override IIdentityLookup GetIdentityLookup(string clusterName)
//         {
//             var identity = new IdentityStorageLookup(new RedisIdentityStorage(clusterName, RedisFixture.Multiplexer));
//             return identity;
//         }
//     }
//
//     public class ChaosMonkeyRedisIdentityClusterFixture : BaseInMemoryClusterFixture
//     {
//         public ChaosMonkeyRedisIdentityClusterFixture() : base(3)
//         {
//         }
//
//         protected override IIdentityLookup GetIdentityLookup(string clusterName)
//         {
//             var identity = new IdentityStorageLookup(
//                 new FailureInjectionStorage(new RedisIdentityStorage(clusterName, RedisFixture.Multiplexer))
//             );
//             return identity;
//         }
//
//         public class RedisClusterTests : ClusterTests, IClassFixture<RedisIdentityClusterFixture>
//         {
//             // ReSharper disable once SuggestBaseTypeForParameter
//             public RedisClusterTests(ITestOutputHelper testOutputHelper, RedisIdentityClusterFixture clusterFixture)
//                 : base(testOutputHelper, clusterFixture)
//             {
//             }
//         }
//
//         public class ResilienceRedisClusterTests : ClusterTests, IClassFixture<ChaosMonkeyRedisIdentityClusterFixture>
//         {
//             // ReSharper disable once SuggestBaseTypeForParameter
//             public ResilienceRedisClusterTests(
//                 ITestOutputHelper testOutputHelper,
//                 ChaosMonkeyRedisIdentityClusterFixture clusterFixture
//             )
//                 : base(testOutputHelper, clusterFixture)
//             {
//             }
//         }
//
//         public class RedisStorageTests : IdentityStorageTests
//         {
//             public RedisStorageTests(ITestOutputHelper testOutputHelper) : base(Init, testOutputHelper)
//             {
//             }
//
//             private static IIdentityStorage Init(string clusterName)
//                 =>
//                     new RedisIdentityStorage(clusterName,
//                         RedisFixture.Multiplexer,
//                         TimeSpan.FromMilliseconds(1500)
//                     );
//         }
//     }
//
//     static class RedisFixture
//     {
//         static RedisFixture() => ThreadPool.SetMinThreads(250, 250);
//
//         private static readonly Lazy<ConnectionMultiplexer> LazyConnection = new(() 
//             => ConnectionMultiplexer.Connect(TestConfig.Configuration.GetConnectionString("Redis")));
//
//         public static ConnectionMultiplexer Multiplexer => LazyConnection.Value;
//     }
// }