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
            Console.WriteLine("Using RedisLookup 222");
            /*
             *  docker build . -t rogeralsing/kubdiagg   
             *  kubectl apply --filename service.yaml    
             *  kubectl get pods -l app=kubdiag
             *  kubectl logs -l app=kubdiag --all-containers
             * 
             */

            var l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Critical));
            Log.SetLoggerFactory(l);
            var log = Log.CreateLogger("main");

            var identity = new PartitionIdentityLookup(TimeSpan.FromSeconds(2),TimeSpan.FromSeconds(2));//  new IdentityStorageLookup(GetRedisId("MyCluster"));

            var port = int.Parse(Environment.GetEnvironmentVariable("PROTOPORT")!);
            var host = Environment.GetEnvironmentVariable("PROTOHOST");
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            log.LogInformation("Host {host}", host);
            log.LogInformation("Port {port}", port);
            log.LogInformation("Advertised Host {advertisedHost}", advertisedHost);

            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            //
            var clusterprovider = new KubernetesProvider(kubernetes, new KubernetesProviderConfig(10,false));

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

                Console.WriteLine($"My members {hash}");

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

                Console.WriteLine($"Consensus {res}");

                await Task.Delay(3000);
            }
            

            Thread.Sleep(Timeout.Infinite);
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