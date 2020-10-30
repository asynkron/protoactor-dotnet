using System;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Tests;

namespace Proto.Cluster.MongoIdentityLookup.Tests
{
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
}