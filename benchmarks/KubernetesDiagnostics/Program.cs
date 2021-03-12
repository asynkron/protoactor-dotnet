using System;
using System.Threading;
using k8s;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Events;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcCore;

namespace KubernetesDiagnostics
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Debug));
            Log.SetLoggerFactory(l);
            var log = Log.CreateLogger("main");

            var db = GetMongo();
            var identity = new IdentityStorageLookup(new MongoIdentityStorage("mycluster", db.GetCollection<PidLookupEntity>("pids"), 200));
            
            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            var clusterprovider = new KubernetesProvider(kubernetes);

            var port = int.Parse(Environment.GetEnvironmentVariable("PROTOPORT"));
            var host = Environment.GetEnvironmentVariable("PROTOHOST");
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            var system = new ActorSystem()
                .WithRemote(GrpcCoreRemoteConfig
                    .BindTo(host, port)
                    .WithAdvertisedHost(advertisedHost))
                .WithCluster(ClusterConfig
                    .Setup("mycluster", clusterprovider, identity));

            system.EventStream.Subscribe<ClusterTopologyEvent>(x => log.LogInformation("Topology {Topology}",x));
            
            system
                .Cluster()
                .StartMemberAsync();
            
            log.LogInformation("Running....");
            
            Thread.Sleep(Timeout.Infinite);
        }

        private static IMongoDatabase GetMongo()
        {
            var connectionString =
                Environment.GetEnvironmentVariable("MONGO");
            var url = MongoUrl.Create(connectionString);
            var settings = MongoClientSettings.FromUrl(url);
            // settings.WaitQueueSize = 10000;
            // settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            //
            // settings.WriteConcern = WriteConcern.WMajority;
            // settings.ReadConcern = ReadConcern.Majority;
            var client = new MongoClient(settings);
            var database = client.GetDatabase("ProtoMongo");
            return database;
        }
    }
}