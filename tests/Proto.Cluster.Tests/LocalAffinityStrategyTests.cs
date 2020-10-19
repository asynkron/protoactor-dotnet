using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using MongoDB.Driver;
using Proto.Cluster.IdentityLookup;
using Proto.Remote.Tests.Messages;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests
{
    public class LocalAffinityStrategyTests : ClusterTests
    {
        
        public LocalAffinityStrategyTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }


        [Fact]
        public async Task PrefersLocalPlacement()
        {
            var clusters = await SpawnMembers(2);
            await Task.Delay(1000);
            var timeout = new CancellationTokenSource(30000).Token;

            var firstNode = clusters[0];

            await PingAll(firstNode);

            var secondNode = clusters[1];
            firstNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(1000, 
                "We expect the actors to be localized to the node receiving traffic.");
            secondNode.System.ProcessRegistry.ProcessCount.Should().BeLessThan(100);

            TestOutputHelper.WriteLine($"Actors: first node: {firstNode.System.ProcessRegistry.ProcessCount}, second node: {secondNode.System.ProcessRegistry.ProcessCount}");

            await PingAll(secondNode);
            await PingAll(secondNode);
            await PingAll(secondNode);


            await Task.Delay(500);
            TestOutputHelper.WriteLine("After traffic is shifted to second node:");
            TestOutputHelper.WriteLine($"Actors: first node: {firstNode.System.ProcessRegistry.ProcessCount}, second node: {secondNode.System.ProcessRegistry.ProcessCount}");

            firstNode.System.ProcessRegistry.ProcessCount.Should().BeInRange(100,1000,
                "Some actors should have moved to the new node");
            
            secondNode.System.ProcessRegistry.ProcessCount.Should().BeGreaterThan(100,
                "When traffic shifts to the second node, actors receiving remote traffic should start draining from the original node and be recreated");
            
            async Task PingAll(Cluster cluster)
            {
                foreach (var i in Enumerable.Range(0, 1000))
                {
                    Pong pong = null;
                    while (pong == null)
                    {
                        timeout.ThrowIfCancellationRequested();
                        pong = await cluster.Ping(i.ToString(), "hello", timeout);
                    }
                }

                // await Task.WhenAll(
                // Enumerable.Range(0, 1000).Select(async i =>
                //     {
                //         Pong pong = null;
                //         while (pong == null)
                //         {
                //             timeout.Token.ThrowIfCancellationRequested();
                //             pong = await cluster.Ping(i.ToString(), "hello", timeout.Token);
                //         }
                //     }
                // ));
            }
        }

        protected override ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, string clusterName,
            IIdentityLookup identityLookup) =>
            base.GetClusterConfig(clusterProvider, clusterName, identityLookup)
                .WithClusterKind(EchoActor.Kind, EchoActor.Props.WithPoisonOnRemoteTraffic(.5f))
                .WithRemoteConfig(config => config.WithProtoMessages(Remote.Tests.Messages.ProtosReflection.Descriptor))
                .WithMemberStrategyBuilder((cluster, kind) => new LocalAffinityStrategy(cluster, 1100));
        
        // protected override IIdentityLookup GetIdentityLookup(string clusterName)
        // {
        //     var db = GetMongo();
        //     var identity = new MongoIdentityLookup.MongoIdentityLookup(clusterName, db);
        //     return identity;
        // }


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