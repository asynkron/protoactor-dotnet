using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Divergic.Logging.Xunit;
using FluentAssertions;
using MongoDB.Driver;
using Proto.Cluster.Consul;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
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
            var factory = LogFactory.Create(testOutputHelper);
            _testOutputHelper = testOutputHelper;
            Log.SetLoggerFactory(factory);
        }

        [Theory (Skip = "Requires Consul and Mongo to be available on localhost")]
        [InlineData(1, 100, 10, true)]
        [InlineData(3, 100, 10, true)]
        // [InlineData(2, 1, 1, false)]
        // [InlineData(3, 100, 10, false)]
        public async Task MongoIdentityClusterTest(int clusterNodes, int sendingActors, int messagesSentPerCall,
            bool useMongoIdentity)
        {
            const string aggregatorId = "agg-1";
            var clusterMembers = await SpawnMembers(clusterNodes, useMongoIdentity);

            await Task.Delay(1000);
            
            var maxWait = new CancellationTokenSource(5000);

            var sendRequestsSent = clusterMembers.SelectMany(
                cluster =>
                {
                    return Enumerable.Range(0, sendingActors)
                        .Select(id => cluster
                            .RequestAsync<Ack>($"snd-{id}", "sender", new SendToRequest
                                {
                                    Count = messagesSentPerCall,
                                    Id = aggregatorId
                                }, maxWait.Token
                            )
                        );
                }
            ).ToList();

            await Task.WhenAll(sendRequestsSent);
            
            var result = await clusterMembers.First().RequestAsync<AggregatorResult>(aggregatorId, "aggregator",
                new AskAggregator(),
                new CancellationTokenSource(5000).Token
            );

            result.Should().NotBeNull("We expect a response from the aggregator actor");
            result.SequenceKeyCount.Should().Be(sendRequestsSent.Count, "We expect a unique id per send request");
            result.SenderKeyCount.Should().Be(sendingActors, "We expect a single instantiation per sender id");
            result.OutOfOrderCount.Should().Be(0, "Messages from one actor to another should be received in order");
            result.TotalMessages.Should().Be(sendRequestsSent.Count * messagesSentPerCall);
        }

        private async Task<IList<Cluster>> SpawnMembers(int memberCount, bool useMongoIdentity)
        {
            var clusterName = "test-cluster." + Guid.NewGuid().ToString("N");
            var clusterTasks = Enumerable.Range(0, memberCount).Select(_ => SpawnMember(clusterName, useMongoIdentity))
                .ToList();
            await Task.WhenAll(clusterTasks);
            return clusterTasks.Select(task => task.Result).ToList();
        }

        private async Task<Cluster> SpawnMember(string clusterName, bool useMongoIdentity)
        {
            var system = new ActorSystem();
            var clusterProvider = new ConsulProvider(new ConsulProviderConfig());
            var identityLookup = useMongoIdentity ? GetIdentityLookup(clusterName) : new PartitionIdentityLookup();
            
            var senderProps = Props.FromProducer(() => new SenderActor(_testOutputHelper));
            var aggProps = Props.FromProducer(() => new VerifyOrderActor());

            var config = GetClusterConfig(clusterProvider, clusterName, identityLookup)
                .WithClusterKind("sender", senderProps)
                .WithClusterKind("aggregator", aggProps);
           

            var cluster = new Cluster(system, config);
            
            await cluster.StartMemberAsync();
            return cluster;
        }


        private static ClusterConfig GetClusterConfig(IClusterProvider clusterProvider, string clusterName,
            IIdentityLookup identityLookup)
        {
            var port = Environment.GetEnvironmentVariable("PROTOPORT") ?? "0";
            var p = int.Parse(port);
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? "127.0.0.1";
            
            var remoteConfig = new RemoteConfig(host, p)
                .WithProtoMessages(MessagesReflection.Descriptor)
                .WithAdvertisedHostname(Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC") ?? host!);
            
            return new ClusterConfig(clusterName, host, p, clusterProvider)
                .WithIdentityLookup(identityLookup)
                .WithRemoteConfig(remoteConfig);
        }

        private static IIdentityLookup GetIdentityLookup(string clusterName)
        {
            var db = GetMongo();
            var identity = new MongoIdentityLookup(clusterName, db);
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
            private Cluster _cluster;
            private readonly ITestOutputHelper _testOutputHelper;

            private string _instanceId;
            private int _seq;

            public SenderActor(ITestOutputHelper testOutputHelper)
            {
                _testOutputHelper = testOutputHelper;
            }

            public async Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case ClusterInit init:
                        _instanceId = $"{init.Kind}:{init.Identity}.{Guid.NewGuid():N}";
                        _cluster = init.Cluster;
                        break;
                    case SendToRequest sendTo:

                        var key = Guid.NewGuid().ToString("N");
                        for (var i = 0; i < sendTo.Count; i++)
                        {
                            try
                            {
                                await _cluster.RequestAsync<Ack>(sendTo.Id, "aggregator", new SequentialIdRequest
                                    {
                                        SequenceKey = key,
                                        SequenceId = _seq++,
                                        Sender = _instanceId
                                    }, CancellationToken.None
                                );
                            }
                            catch (Exception e)
                            {
                                _testOutputHelper.WriteLine("Failed to send to aggregator: {0}", e);
                            }
                        }

                        context.Respond(new Ack());
                        break;
                }
            }
        }

        private class VerifyOrderActor : IActor
        {
            private int _outOfOrderErrors;
            private int _seqRequests;

            private readonly Dictionary<string, int> _lastReceivedSeq = new Dictionary<string, int>();
            private readonly HashSet<string> _senders = new HashSet<string>();

            public Task ReceiveAsync(IContext context)
            {
                switch (context.Message)
                {
                    case SequentialIdRequest request:
                        HandleOrderedRequest(request, context);
                        break;
                    case AskAggregator _:
                        context.Respond(new AggregatorResult
                            {
                                SequenceKeyCount = _lastReceivedSeq.Count,
                                TotalMessages = _seqRequests,
                                OutOfOrderCount = _outOfOrderErrors,
                                SenderKeyCount = _senders.Count
                            }
                        );
                        break;
                }

                return Actor.Done;
            }

            private void HandleOrderedRequest(SequentialIdRequest request, IContext context)
            {
                _seqRequests++;
                _senders.Add(request.Sender);
                var outOfOrder = _lastReceivedSeq.TryGetValue(request.SequenceKey, out var last) &&
                                 last + 1 != request.SequenceId;
                _lastReceivedSeq[request.SequenceKey] = request.SequenceId;
                if (outOfOrder)
                {
                    _outOfOrderErrors++;
                }

                context.Respond(new Ack());
            }
        }
    }
}