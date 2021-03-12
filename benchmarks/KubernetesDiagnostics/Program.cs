using System;
using k8s;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;

namespace KubernetesDiagnostics
{
    class Program
    {
        static void Main(string[] args)
        {
            var kubernetes = new Kubernetes(KubernetesClientConfiguration.InClusterConfig());
            var clusterprovider = new KubernetesProvider(kubernetes);
              
            var portStr = Environment.GetEnvironmentVariable("PROTOPORT") ?? $"{RemoteConfigBase.AnyFreePort}";
            var port = int.Parse(portStr);
            var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? RemoteConfigBase.Localhost;
            var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");
            
            var remoteConfig = GrpcCoreRemoteConfig
                .BindTo(host, port)
                .WithAdvertisedHost(advertisedHost);

            var clusterConfig = ClusterConfig
                .Setup("mycluster", clusterprovider, new PartitionIdentityLookup());

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);
            
            system.Cluster().StartMemberAsync();
        }
    }
}