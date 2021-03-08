using System;
using k8s;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Console;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Events;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.Remote.GrpcCore;
using Log = Proto.Log;

namespace KubernetesEvents
{
    class Program
    {
        static void Main(string[] args)
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l1 =>
                    l1.AddConsole().SetMinimumLevel(LogLevel.Debug)
                )
            );

            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost();

            var clusterConfig = ClusterConfig
                .Setup("mycluster", ClusterProvider(), new PartitionIdentityLookup())
                .WithClusterKind("Test", Props.Empty);

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            system.EventStream.Subscribe<ClusterTopologyEvent>(x => {
                    Console.WriteLine(x);
                }
            );

            system.Cluster().StartMemberAsync();
            Console.ReadLine();
        }
        
        private static IClusterProvider ClusterProvider()
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
    }
}