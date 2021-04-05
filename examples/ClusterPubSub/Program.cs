using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Remote;
using Proto.Remote.GrpcCore;

namespace ClusterPubSub
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
              .WithProtoMessages(ClusterPubSub.ProtosReflection.Descriptor);

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
                        if (ctx.Message is SomeMessage s)
                        {
                          //  Console.Write(".");
                            ctx.Respond(new PublishResponse());
                        }

                        return Task.CompletedTask;
                    }
                )
            );

            await system.Cluster().Subscribe("my-topic", pid);
            var p = system.Cluster().Producer("my-topic");
            
            Console.WriteLine("starting");

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task>();
            for (int i = 0; i < 100; i++)
            {
                 var t = p.ProduceAsync(new SomeMessage()
                 {
                     Value = i,
                 }); 
                 tasks.Add(t);
            }

            Console.WriteLine("waiting...");
            await Task.WhenAll(tasks);
            tasks.Clear();
            ;
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);
            sw.Restart();
            for (int i = 0; i < 200000; i++)
            {
                tasks.Add(p.ProduceAsync(new SomeMessage
                {
                    Value = i,
                }));
            }

            await Task.WhenAll(tasks);
            tasks.Clear();
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            Console.ReadLine();
        }
    }
}