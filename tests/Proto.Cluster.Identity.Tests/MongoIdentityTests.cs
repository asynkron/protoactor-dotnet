// ReSharper disable UnusedType.Global
using MongoDB.Driver;
using Proto.Cluster.Tests;
using Xunit;
using Xunit.Abstractions;
using Microsoft.Extensions.Configuration;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.IdentityLookup;
using Proto.TestFixtures;

namespace Proto.Cluster.Identity.Tests
{
    public class MongoIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public MongoIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var db = GetMongo();
            var pids = db.GetCollection<PidLookupEntity>("pids");
            var identity = new IdentityStorageLookup(new MongoIdentityStorage(clusterName, pids));
            return identity;
        }

        internal static IMongoDatabase GetMongo()
        {
            var connectionString = TestConfig.Configuration.GetConnectionString("MongoDB");
            var settings = MongoClientSettings.FromConnectionString(connectionString);
            settings.MaxConnectionPoolSize = 200;
            settings.RetryReads = true;
            settings.RetryWrites = true;
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }
    }

    // public class MongoClusterTests : ClusterTests, IClassFixture<MongoIdentityClusterFixture>
    // {
    //     // ReSharper disable once SuggestBaseTypeForParameter
    //     public MongoClusterTests(ITestOutputHelper testOutputHelper, MongoIdentityClusterFixture clusterFixture)
    //         : base(testOutputHelper, clusterFixture)
    //     {
    //     }
    // }
    //
    // public class MongoStorageTests : IdentityStorageTests
    // {
    //     public MongoStorageTests() : base(Init)
    //     {
    //     }
    //
    //     private static IIdentityStorage Init(string clusterName)
    //     {
    //         var db = MongoIdentityClusterFixture.GetMongo();
    //         return new MongoIdentityStorage(clusterName, db.GetCollection<PidLookupEntity>("pids"));
    //     }
    // }
}