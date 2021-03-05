using System;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using ProtosReflection = Proto.Remote.ProtosReflection;

namespace ClusterPubSub
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup())
                    .WithClusterKind("topic",  Props.FromProducer(() => new TopicActor()));

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            var pid = system.Root.Spawn(Props.FromFunc(ctx => {
                    if (ctx.Message is string)
                    {
                        Console.WriteLine(ctx.Message);
                    }
            
                    return Task.CompletedTask;
                }
            ));


            await system.Cluster().Subscribe("my-topic", pid);

        }
    }
}