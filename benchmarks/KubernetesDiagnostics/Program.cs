using System;
using System.Threading;
using k8s;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Events;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;

namespace KubernetesDiagnostics
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var l = LoggerFactory.Create(c => c.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            
            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            var clusterprovider = new KubernetesProvider(kubernetes);
              
            var portStr = Environment.GetEnvironmentVariable("PROTOPORT") ?? $"{RemoteConfigBase.AnyFreePort}";
            var port = int.Parse(portStr);
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? RemoteConfigBase.Localhost;
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

            var system = new ActorSystem()
                .WithRemote(GrpcCoreRemoteConfig
                    .BindTo(host, port)
                    .WithAdvertisedHost(advertisedHost))
                .WithCluster(ClusterConfig
                    .Setup("mycluster", clusterprovider, new PartitionIdentityLookup()));

            system.EventStream.Subscribe<ClusterTopologyEvent>(Console.WriteLine);
            
            system.Cluster().StartMemberAsync();
            
            Console.WriteLine("Runnning...");
            Thread.Sleep(Timeout.Infinite);
        }
    }
}