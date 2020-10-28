using System;
using System.Threading.Tasks;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Tests;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.MongoIdentityLookup.Tests
{
    public class MongoIdentityLookupTests: ClusterTestTemplate
    {
        //Xunit class Skip anyone?
        private const string SkipReason = "Mongo needs to be available on localhost";
        
        public MongoIdentityLookupTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper) { }

        [Theory(Skip = SkipReason)]
        [InlineData(1, 100, 100)]
        public override Task OrderedDeliveryFromActors(int clusterNodes, int sendingActors, int messagesSentPerCall)
        {
            return base.OrderedDeliveryFromActors(clusterNodes, sendingActors, messagesSentPerCall);
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