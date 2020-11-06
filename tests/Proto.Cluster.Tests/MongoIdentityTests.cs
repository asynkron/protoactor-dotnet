namespace Proto.Cluster.Tests
{
    using System;
    using IdentityLookup;
    using MongoDB.Driver;
    using MongoIdentityLookup;

    // ReSharper disable once UnusedType.Global
    public class MongoIdentityClusterFixture : BaseInMemoryClusterFixture
    {
        public MongoIdentityClusterFixture() : base(3)
        {
        }

        protected override IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var db = GetMongo();
            var identity = new MongoIdentityLookup(clusterName, db);
            return identity;
        }

        IMongoDatabase GetMongo()
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

    // public class MongoClusterTests: ClusterTests, IClassFixture<MongoIdentityClusterFixture>
    // {
    //     public MongoClusterTests(ITestOutputHelper testOutputHelper, MongoIdentityClusterFixture clusterFixture) : base(testOutputHelper, clusterFixture)
    //     {
    //     }
    // }
}