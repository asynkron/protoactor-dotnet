using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Gossip;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
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
            Console.WriteLine("123");

            /*
             *  docker build . -t rogeralsing/kubdiagg   
             *  kubectl apply --filename service.yaml    
             *  kubectl get pods -l app=kubdiag
             *  kubectl logs -l app=kubdiag --all-containers
             * 
             */

            var l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            var log = Log.CreateLogger("main");

            var identity = new PartitionIdentityLookup(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(2));//  new IdentityStorageLookup(GetRedisId("MyCluster"));

            
            /*
            - name: "REDIS"
              value: "redis"
            - name: PROTOPORT
              value: "8080"
            - name: PROTOHOST
              value: "0.0.0.0"
            - name: "PROTOHOSTPUBLIC"
             */

            var port = int.Parse(Environment.GetEnvironmentVariable("PROTOPORT") ?? "0");
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? "127.0.0.1";
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            log.LogInformation("Host {host}", host);
            log.LogInformation("Port {port}", port);
            log.LogInformation("Advertised Host {advertisedHost}", advertisedHost);

            var clusterprovider = GetProvider();

            var system = new ActorSystem(new ActorSystemConfig()
                    .WithDeveloperReceiveLogging(TimeSpan.FromSeconds(1))
                    .WithDeveloperSupervisionLogging(true))
                .WithRemote(GrpcNetRemoteConfig
                    .BindTo(host, port)
                    .WithAdvertisedHost(advertisedHost)
                )
                .WithCluster(ClusterConfig
                    .Setup("mycluster", clusterprovider, identity)
                    .WithClusterKind("empty", Props.Empty)
                );

            system.EventStream.Subscribe<GossipUpdate>(e => {
                    Console.WriteLine($"{DateTime.Now:O} Gossip update Member {e.MemberId} Key {e.Key}");
                }
            );
            system.EventStream.Subscribe<ClusterTopology>(e => {
                var members = e.Members;
                var x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
                var key = string.Join("", x);
                var hash = MurmurHash2.Hash(key);

                Console.WriteLine($"{DateTime.Now:O} My members {hash}");

                // foreach (var member in members.OrderBy(m => m.Id))
                // {
                //     Console.WriteLine(member.Id + "\t" + member.Address + "\t" + member.Kinds);
                // }
            }
            );

            await system
                .Cluster()
                .StartMemberAsync();


            while (true)
            {
                var res = await system.Cluster().MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));

                var m = system.Cluster().MemberList.GetAllMembers();
                var hash = Member.TopologyHash(m);
                
                Console.WriteLine($"{DateTime.Now:O} Consensus {res} Hash {hash} Count {m.Length}");

                await Task.Delay(3000);
            }
            

            Thread.Sleep(Timeout.Infinite);
        }

        private static IClusterProvider GetProvider()
        {
            try
            {
                var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
                Console.WriteLine("Running with Kubernetes Provider");
                return new KubernetesProvider(kubernetes);
            }
            catch
            {
                Console.WriteLine("Running with Consul Provider");
                return new ConsulProvider(new ConsulProviderConfig());
            }
        }

        private static IIdentityStorage GetRedisId(string clusterName)
        {
            var connectionString =
                Environment.GetEnvironmentVariable("REDIS");

            Console.WriteLine("REDIS " + connectionString);

            var multiplexer = ConnectionMultiplexer.Connect(connectionString);
            var identity = new RedisIdentityStorage(clusterName, multiplexer);
            return identity;
        }
    }
}