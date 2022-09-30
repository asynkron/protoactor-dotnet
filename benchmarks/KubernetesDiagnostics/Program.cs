using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace KubernetesDiagnostics;

public static class Program
{
    public static async Task Main()
    {
        ThreadPool.SetMinThreads(100, 100);
        Console.WriteLine("Starting...");

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

        var identity = new PartitionIdentityLookup(TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(2)
        ); //  new IdentityStorageLookup(GetRedisId("MyCluster"));

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

        var clusterProvider = GetProvider();

        var noOpsProps = Props.FromFunc(ctx => Task.CompletedTask);
        var echoKind = new ClusterKind("echo", noOpsProps);
        var system = new ActorSystem(new ActorSystemConfig()
                //     .WithDeveloperReceiveLogging(TimeSpan.FromSeconds(1))
                //     .WithDeveloperSupervisionLogging(true)
            )
            .WithRemote(GrpcNetRemoteConfig
                .BindTo(host, port)
                .WithAdvertisedHost(advertisedHost)
                .WithEndpointWriterMaxRetries(2)
            )
            .WithCluster(ClusterConfig
                .Setup("mycluster", clusterProvider, identity)
                .WithClusterKind("empty", Props.Empty)
                .WithClusterKind(echoKind)
            );

        system.EventStream.Subscribe<GossipUpdate>(e => { Console.WriteLine($"{DateTime.Now:O} Gossip update Member {e.MemberId} Key {e.Key}"); }
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

        var cts = new CancellationTokenSource();

        Console.CancelKeyPress += (_, _) => { cts.Cancel(); };

        await system
            .Cluster()
            .StartMemberAsync();

        var props = Props.FromFunc(ctx => Task.CompletedTask);
        system.Root.SpawnNamed(props, "dummy");

        var clusterIdentity = ClusterIdentity.Create("some-id", echoKind.Name);

        while (!cts.IsCancellationRequested)
        {
            // var res = await system.Cluster().MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));

            var m = system.Cluster().MemberList.GetAllMembers();
            var hash = Member.TopologyHash(m);

            Console.WriteLine($"{DateTime.Now:O} Hash {hash} Count {m.Length}");

            try
            {
                var t = await system.Cluster().RequestAsync<Touched>(clusterIdentity, new Touch(), CancellationTokens.FromSeconds(1));

                if (t != null)
                {
                    Console.WriteLine($"called cluster actor {t.Who}");
                }
                else
                {
                    Console.WriteLine($"call to cluster actor returned null");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Could not call cluster actor: {e}");
            }

            foreach (var member in m)
            {
                var pid = new PID(member.Address, "dummy");

                try
                {
                    var t = await system.Root.RequestAsync<Touched>(pid, new Touch(), CancellationTokens.FromSeconds(1));

                    if (t != null)
                    {
                        Console.WriteLine($"called dummy actor {pid}");
                    }
                    else
                    {
                        Console.WriteLine($"call to dummy actor timed out {pid}");
                    }
                }
                catch
                {
                    Console.WriteLine($"Could not call dummy actor {pid}");
                }
            }

            await Task.Delay(3000);
        }
        
        await system
            .Cluster()
            .ShutdownAsync();
    }

    private static IClusterProvider GetProvider() => new KubernetesProvider();
}