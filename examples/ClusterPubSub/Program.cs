using System;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Remote.GrpcCore;

namespace ClusterPubSub
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost();
            //  .WithProtoMessages(ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig());

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup())
                    .WithClusterKind("topic", Props.FromProducer(() => new TopicActor()));

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            var pid = system.Root.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is PublishRequest)
                        {
                            Console.WriteLine(ctx.Message);
                            ctx.Respond(new PublishResponse());
                        }

                        return Task.CompletedTask;
                    }
                )
            );


            await system.Cluster().Subscribe("my-topic", pid);

            for (int i = 0; i < 100; i++)
            {
                 await system.Cluster().Publish("my-topic", "hello");    
            }

            Console.ReadLine();
        }
    }
}