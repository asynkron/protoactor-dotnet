// ReSharper disable UnusedType.Global
namespace Proto.Cluster.Identity.Tests
{
    using System;
    using IdentityLookup;
    using MongoDb;
    using MongoDB.Driver;
    using Proto.Cluster.Tests;
    using Xunit;
    using Xunit.Abstractions;

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
            var connectionString =
                Environment.GetEnvironmentVariable("MONGO") ?? "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
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