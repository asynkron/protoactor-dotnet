using System;
using System.Diagnostics;
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
            
            Console.WriteLine("123");

            var pid = system.Root.Spawn(Props.FromFunc(ctx => {
                        if (ctx.Message is string s)
                        {
                          //  Console.Write(".");
                            ctx.Respond(new PublishResponse());
                        }

                        return Task.CompletedTask;
                    }
                )
            );

            await system.Cluster().Subscribe("my-topic", pid);
            var p = system.Cluster().Producer();
            
            Console.WriteLine("starting");

            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 20000; i++)
            {
                 p.Produce("my-topic", i.ToString());    
            }

            await p.WhenAllPublished();
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);
            sw.Restart();
            for (int i = 0; i < 20000; i++)
            {
                p.Produce("my-topic", i.ToString());    
            }

            await p.WhenAllPublished();
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            Console.ReadLine();
        }
    }
}