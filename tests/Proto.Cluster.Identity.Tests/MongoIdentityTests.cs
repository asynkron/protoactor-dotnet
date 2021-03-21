// ReSharper disable UnusedType.Global
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Tests;
using Proto.TestFixtures;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Identity.Tests
{
    public class MongoIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public MongoIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var pids = MongoFixture.Database.GetCollection<PidLookupEntity>("pids");
            var identity = new IdentityStorageLookup(new MongoIdentityStorage(clusterName, pids));
            return identity;
        }

        public class MongoClusterTests : ClusterTests, IClassFixture<MongoIdentityClusterFixture>
        {
            // ReSharper disable once SuggestBaseTypeForParameter
            public MongoClusterTests(ITestOutputHelper testOutputHelper, MongoIdentityClusterFixture clusterFixture)
                : base(testOutputHelper, clusterFixture)
            {
            }
        }
    }

    public class ChaosMongoIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public ChaosMongoIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var pids = MongoFixture.Database.GetCollection<PidLookupEntity>("pids");
            var identity = new IdentityStorageLookup(new FailureInjectionStorage(new MongoIdentityStorage(clusterName, pids)));
            return identity;
        }

        public class ChaosMongoClusterTests : ClusterTests, IClassFixture<ChaosMongoIdentityClusterFixture>
        {
            // ReSharper disable once SuggestBaseTypeForParameter
            public ChaosMongoClusterTests(ITestOutputHelper testOutputHelper, ChaosMongoIdentityClusterFixture clusterFixture)
                : base(testOutputHelper, clusterFixture)
            {
            }
        }
    }

    static class MongoFixture
    {
        static MongoFixture()
        {
            var connectionString = TestConfig.Configuration.GetConnectionString("MongoDB");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.MaxConnectionPoolSize = 200;
            settings.RetryReads = true;
            settings.RetryWrites = true;
            Client = new MongoClient(settings);
            Database = Client.GetDatabase("ProtoMongo");
        }

        public static IMongoDatabase Database { get; }

        public static MongoClient Client { get; }
    }

    public class MongoStorageTests : IdentityStorageTests
    {
        public MongoStorageTests(ITestOutputHelper testOutputHelper) : base(Init, testOutputHelper)
        {
        }

        private static IIdentityStorage Init(string clusterName)
            => new MongoIdentityStorage(clusterName, MongoFixture.Database.GetCollection<PidLookupEntity>("pids"));
    }
}