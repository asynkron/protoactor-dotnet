using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using MongoDB.Driver;
using Proto.Cluster.Consul;
using Proto.Remote;
using Xunit;
using Xunit.Abstractions;

namespace Proto.Cluster.MongoIdentityLookup.Tests
{
    public class MongoClusterTest
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public MongoClusterTest(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Theory]
        [InlineData(1, 100, 100)]
        [InlineData(2, 10, 10)]
        public async Task MongoIdentityClusterTest(int clusterNodes, int sendingActors, int messagesSentPerCall)
        {
            const string aggregatorId = "agg1";
            var clusterMembers = await SpawnMembers(clusterNodes);

            var maxWait = new CancellationTokenSource(5000);

            _testOutputHelper.WriteLine("Sending");
            var sendersToldToSend = clusterMembers.SelectMany(
                cluster =>
                {
                    return Enumerable.Range(0, sendingActors)
                        .Select(id => cluster
                            .RequestAsync<bool>(id.ToString(), "sender", new SendToRequest
                                {
                                    Count = messagesSentPerCall,
                                    Id = aggregatorId
                                }, maxWait.Token
                            )
                        );
                }
            ).ToList();

            try
            {
                await Task.WhenAll(sendersToldToSend);
                _testOutputHelper.WriteLine("All responded");
            }
            catch (TimeoutException)
            {
                _testOutputHelper.WriteLine("Timed out");
            }

            var result = await clusterMembers.First().RequestAsync<AggregatorResult>(aggregatorId, "aggregator", new AskAggregator(),
                new CancellationTokenSource(1000).Token
            );

            result.DifferentKeys.Should().Be(sendersToldToSend.Count);
            result.OutOfOrder.Should().Be(0);
            result.TotalMessages.Should().Be(sendersToldToSend.Count * messagesSentPerCall);
        }

        private async Task<IList<Cluster>> SpawnMembers(int memberCount)
        {
            var clusterTasks = Enumerable.Range(0, memberCount).Select(_ => SpawnMember()).ToList();
            await Task.WhenAll(clusterTasks);
            return clusterTasks.Select(task => task.Result).ToList();
        }

        private async Task<Cluster> SpawnMember()
        {
            var system = new ActorSystem();
            var clusterProvider = new ConsulProvider(new ConsulProviderOptions());
            var serialization = new Serialization();
            serialization.RegisterFileDescriptor(MessagesReflection.Descriptor);
            var identity = GetIdentityLookup();
            var cluster = new Cluster(system, serialization);

            var senderProps = Props.FromProducer(() =>
                {
                    _testOutputHelper.WriteLine("Constructing sender");
                    return new SenderActor(cluster, _testOutputHelper);
                }
            );

            var aggProps = Props.FromProducer(() =>
                {
                    _testOutputHelper.WriteLine("Constructing aggregator");

                    return new VerifyOrderActor();
                }
            );

            cluster.Remote.RegisterKnownKind("sender", senderProps);
            cluster.Remote.RegisterKnownKind("aggregator", aggProps);

            var config = GetClusterConfig(clusterProvider, identity);
            await cluster.StartMemberAsync(config);
            return cluster;
        }


        private static ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, MongoIdentityLookup identity)
        {
            var port = Environment.GetEnvironmentVariable("PROTOPORT") ?? "0";
            var p = int.Parse(port);
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? "127.0.0.1";
            var remote = new RemoteConfig();

            var advertiseHostname = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC") ?? host;
            remote.AdvertisedHostname = advertiseHostname!;

            return new ClusterConfig("test-cluster", host, p, clusterProvider).WithIdentityLookup(identity)
                .WithRemoteConfig(remote);
        }

        private static MongoIdentityLookup GetIdentityLookup()
        {
            var db = GetMongo();
            var identity = new MongoIdentityLookup("mycluster", db);
            return identity;
        }

        static IMongoDatabase GetMongo()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("MONGO") ?? "mongodb://127.0.0.1:27017/ProtoMongo";
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }

        private class SenderActor : IActor
        {
            private readonly Cluster _cluster;
            private readonly ITestOutputHelper _testOutputHelper;

            private string _id;
            private int _seq = 0;

            public SenderActor(Cluster cluster, ITestOutputHelper testOutputHelper)
            {
                _cluster = cluster;
                _testOutputHelper = testOutputHelper;
            }

            public async Task ReceiveAsync(IContext context)
            {
                _testOutputHelper.WriteLine("Receiving " + context.Message);
                switch (context.Message)
                {
                    case GrainInit init:
                        _id = init.Kind + ":" + init.Identity;
                        break;
                    case SendToRequest sendTo:

                        var key = Guid.NewGuid().ToString("N");
                        for (int i = 0; i < sendTo.Count; i++)
                        {
                            try
                            {
                                await _cluster.RequestAsync<bool>(sendTo.Id, "aggregator", new SequentialIdRequest
                                    {
                                        Key = key,
                                        SequenceId = _seq++
                                    }, CancellationToken.None
                                );
                            }
                            catch (Exception e)
                            {
                                _testOutputHelper.WriteLine("Failed to send to aggregator: {0}", e);
                            }
                        }

                        context.Respond(true);
                        break;
                }
            }
        }
    }
}