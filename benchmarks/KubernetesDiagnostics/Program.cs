using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.Collections;
using k8s;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.MongoDb;
using Proto.Cluster.Kubernetes;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace KubernetesDiagnostics
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            /*
             *  docker build . -t rogeralsing/kubdiagg   
             *  kubectl apply --filename service.yaml    
             *  kubectl get pods -l app=kubdiag
             *  kubectl logs -l app=kubdiag --all-containers
             * 
             */

            ILoggerFactory l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Error));
            Log.SetLoggerFactory(l);
            ILogger log = Log.CreateLogger("main");

            IMongoDatabase db = GetMongo();
            IdentityStorageLookup identity =
                new IdentityStorageLookup(new MongoIdentityStorage("mycluster",
                    db.GetCollection<PidLookupEntity>("pids"), 200));

            Kubernetes kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            KubernetesProvider clusterprovider = new KubernetesProvider(kubernetes);

            int port = int.Parse(Environment.GetEnvironmentVariable("PROTOPORT")!);
            string? host = Environment.GetEnvironmentVariable("PROTOHOST");
            string? advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            log.LogInformation("Host {host}", host);
            log.LogInformation("Port {port}", port);
            log.LogInformation("Advertised Host {advertisedHost}", advertisedHost);

            ActorSystem system = new ActorSystem()
                .WithRemote(GrpcNetRemoteConfig
                    .BindTo(host, port)
                    .WithAdvertisedHost(advertisedHost)
                )
                .WithCluster(ClusterConfig
                    .Setup("mycluster", clusterprovider, identity)
                    .WithClusterKind("empty", Props.Empty)
                );

            system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    RepeatedField<Member> members = e.Members;
                    string[] x = members.Select(m => m.Id).OrderBy(i => i).ToArray();
                    string key = string.Join("", x);
                    uint hash = MurmurHash2.Hash(key);

                    Console.WriteLine("My members " + hash);

                    foreach (Member member in members.OrderBy(m => m.Id))
                    {
                        Console.WriteLine(member.Id + "\t" + member.Address + "\t" + member.Kinds);
                    }
                }
            );

            await system
                .Cluster()
                .StartMemberAsync();

            Thread.Sleep(Timeout.Infinite);
        }

        private static IMongoDatabase GetMongo()
        {
            string? connectionString =
                Environment.GetEnvironmentVariable("MONGO");
            MongoUrl url = MongoUrl.Create(connectionString);
            MongoClientSettings settings = MongoClientSettings.FromUrl(url);
            // settings.WaitQueueSize = 10000;
            // settings.WaitQueueTimeout = TimeSpan.FromSeconds(10);
            //
            // settings.WriteConcern = WriteConcern.WMajority;
            // settings.ReadConcern = ReadConcern.Majority;
            MongoClient client = new MongoClient(settings);
            IMongoDatabase database = client.GetDatabase("ProtoMongo");
            return database;
        }
    }
}
