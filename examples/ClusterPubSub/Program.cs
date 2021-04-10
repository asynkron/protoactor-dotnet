using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Utils;

namespace ClusterPubSub
{
    class Program
    {
        static async Task Main(string[] args)
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l =>
                    l.AddConsole().SetMinimumLevel(LogLevel.Information)
                )
            );
            
            //starting remote node...
            await RunMember();
                
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ClusterPubSub.ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig());

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup())
                    .WithPubSubBatchSize(10000);

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartClientAsync();

            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is SomeMessage s)
                    {
                        //Console.Write(".");
                    }

                    return Task.CompletedTask;
                }
            );
            
            var pid1 = system.Root.Spawn(props);
            var pid2 = system.Root.Spawn(props);

            //subscribe the pid to the my-topic
            await system.Cluster().Subscribe("my-topic", pid1);
            await system.Cluster().Subscribe("my-topic", pid2);
            
            //get hold of a producer that can send messages to the my-topic
            var p = system.Cluster().Producer("my-topic");

            Console.WriteLine("starting");

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task>();

            for (int i = 0; i < 100; i++)
            {
                var t = p.ProduceAsync(new SomeMessage
                    {
                        Value = i,
                    }
                );
                tasks.Add(t);
            }

            Console.WriteLine("waiting...");
            await Task.WhenAll(tasks);
            tasks.Clear();
            ;
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);
            sw.Restart();

            for (int i = 0; i < 1_000_000; i++)
            {
                tasks.Add(p.ProduceAsync(new SomeMessage
                        {
                            Value = i,
                        }
                    )
                );
            }

            await Task.WhenAll(tasks);
            tasks.Clear();
            Console.WriteLine(sw.Elapsed.TotalMilliseconds);

            Console.ReadLine();
        }

        public static async Task RunMember()
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ClusterPubSub.ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig());

            //use an empty store, no persistence
            var store = new EmptyKeyValueStore<Subscribers>();

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup())
                    .WithClusterKind("topic", Props.FromProducer(() => new TopicActor(store)))
                    .WithPubSubBatchSize(10000);

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();
        }
    }
}