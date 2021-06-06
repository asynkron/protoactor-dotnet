using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using StackExchange.Redis;

namespace KubernetesDiagnostics
{
    public static class Program
    {
        public static async Task Main()
        {
            Console.WriteLine("Starting...");
            Console.WriteLine("Using RedisLookup 123");
            /*
             *  docker build . -t rogeralsing/kubdiagg   
             *  kubectl apply --filename service.yaml    
             *  kubectl get pods -l app=kubdiag
             *  kubectl logs -l app=kubdiag --all-containers
             * 
             */

            var l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Error));
            Log.SetLoggerFactory(l);
            var log = Log.CreateLogger("main");

            //  var db = GetMongo();
            var identity = new IdentityStorageLookup(GetRedisId("MyCluster"));

            var port = int.Parse(Environment.GetEnvironmentVariable("PROTOPORT")!);
            var host = Environment.GetEnvironmentVariable("PROTOHOST");
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            log.LogInformation("Host {host}", host);
            log.LogInformation("Port {port}", port);
            log.LogInformation("Advertised Host {advertisedHost}", advertisedHost);

            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            //, new KubernetesProviderConfig(10,true)
            var clusterprovider = new KubernetesProvider(kubernetes);

            var system = new ActorSystem()
                .WithRemote(GrpcNetRemoteConfig
                    .BindTo(host, port)
                    .WithAdvertisedHost(advertisedHost)
                )
                .WithCluster(ClusterConfig
                    .Setup("mycluster", clusterprovider, identity)
                    .WithClusterKind("empty", Props.Empty)
                );

            system.EventStream.Subscribe<ClusterTopology>(e => {
                    var members = e.Members;
                    var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
                    var key = string.Join("", x);
                    var hash = MurmurHash2.Hash(key);

                    Console.WriteLine("My members " + hash);

                    foreach (var member in members.OrderBy(m => m.Id))
                    {
                        Console.WriteLine(member.Id + "\t" + member.Address + "\t" + member.Kinds);
                    }
                }
            );

            await system
                .Cluster()
                .StartMemberAsync();

            _ = Task.Run(async () => {
                    while (true)
                    {
                        await Task.Delay(5000);

                        var t1 = system.Cluster().MemberList.TopologyConsensus();
                        var t2 = Task.Delay(5000);
                        await Task.WhenAny(t1, t2);
                        if (t1.IsCompleted)
                            Console.WriteLine("Consensus reached " + system.Cluster().MemberList.GetAllMembers().Length);
                        else
                            Console.WriteLine("Consensus timeout...");
                    }
                }
            );

            Thread.Sleep(Timeout.Infinite);
        }

        private static IIdentityStorage GetRedisId(string clusterName)
        {
            var options = new ConfigurationOptions()
            {
                
            };

            var multiplexer = ConnectionMultiplexer.Connect(options);
            var identity = new RedisIdentityStorage(clusterName, multiplexer);
            return identity;
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