using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Identity;
using Proto.Cluster.Identity.Redis;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Utils;
using StackExchange.Redis;

namespace ClusterPubSub
{
    class Program
    {
        public static int batchSize = 2000; 
        static async Task Main()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(l =>
                    l.AddConsole().SetMinimumLevel(LogLevel.Information)
                )
            );
            Console.WriteLine("1) Run local");
            Console.WriteLine("2) Run remote");
            var runRemote = Console.ReadLine() == "2";
            
            Console.WriteLine("Subscriber Count, default 10");

            if (!int.TryParse(Console.ReadLine(), out var subscriberCount))
            {
                subscriberCount = 10;
            }
            
            var system = GetSystem();

            if (runRemote)
            {
                await RunMember(); //start the subscriber node
                
                await system
                    .Cluster()
                    .StartClientAsync();
            }
            else
            {
                await system
                    .Cluster()
                    .StartMemberAsync();
            }

            var props = Props.FromFunc(ctx => {
                    if (ctx.Message is SomeMessage s)
                    {
                 //       Console.Write(".");
                    }

                    return Task.CompletedTask;
                }
            );

            for (int j = 0; j < subscriberCount; j++)
            {
                var pid1 = system.Root.Spawn(props);
                //subscribe the pid to the my-topic
                await system.Cluster().Subscribe("my-topic", pid1);
            }

            //get hold of a producer that can send messages to the my-topic
            var p = system.Cluster().Producer("my-topic");

            Console.WriteLine("starting");

            var sw = Stopwatch.StartNew();
            var tasks = new List<Task>();

            for (var i = 0; i < 100; i++)
            {
                var t = p.ProduceAsync(new SomeMessage
                    {
                        Value = i,
                    }
                );
                tasks.Add(t);
            }

       
            await Task.WhenAll(tasks);
            tasks.Clear();
            ;
            
            sw.Restart();

            Console.WriteLine("Running...");
            var messageCount = 1_000_000;

            for (int i = 0; i < messageCount; i++)
            {
                tasks.Add(p.ProduceAsync(new SomeMessage
                        {
                            Value = i,
                        }
                    )
                );
            }

            await Task.WhenAll(tasks);
            sw.Stop();

            var tps = (messageCount * subscriberCount) / sw.ElapsedMilliseconds * 1000;
            Console.WriteLine($"Time {sw.Elapsed.TotalMilliseconds}");
            Console.WriteLine($"Messages per second {tps:N0}");
        }

        private static ActorSystem GetSystem() => new ActorSystem()
            .WithRemote(GetRemoteConfig())
            .WithCluster(GetClusterConfig());

        private static GrpcCoreRemoteConfig GetRemoteConfig() => GrpcCoreRemoteConfig
            .BindToLocalhost()
            .WithProtoMessages(ClusterPubSub.ProtosReflection.Descriptor);

        private static ClusterConfig GetClusterConfig()
        {
            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig());

            //use an empty store, no persistence
            var store = new EmptyKeyValueStore<Subscribers>();
            
            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, GetRedisIdentityLookup())
                    .WithClusterKind("topic", Props.FromProducer(() => new TopicActor(store)))
                    .WithPubSubBatchSize(batchSize);
            return clusterConfig;
        }
        
        private static IIdentityLookup GetRedisIdentityLookup()
        {
            var multiplexer = ConnectionMultiplexer.Connect("localhost:6379");
            var redisIdentityStorage = new RedisIdentityStorage("mycluster", multiplexer,maxConcurrency:50);

            return new IdentityStorageLookup(redisIdentityStorage);
        }

        public static async Task RunMember()
        {
            var system = GetSystem();
            await system
                .Cluster()
                .StartMemberAsync();
            
            Console.WriteLine("Started worker node...");
        }
    }
}