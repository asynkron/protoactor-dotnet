// -----------------------------------------------------------------------
// <copyright file="Foo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Some.Namespace;

namespace ActorMetrics
{
    public static class RunDummyCluster
    {
        public static void Run()
        {
            var l = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            var config = ActorSystemConfig.Setup().WithMetrics();

            var remoteConfig = GrpcNetRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(MessagesReflection.Descriptor)
                .WithProtoMessages(SeedContractsReflection.Descriptor);

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", new SeedNodeClusterProvider(),
                        new PartitionIdentityLookup()
                    );

            var system = new ActorSystem(config)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);
            
            Console.WriteLine($"System 1 Id {system.Id}");

            var memberCluster1 = system
                .Cluster();

            memberCluster1
                .StartMemberAsync()
                .GetAwaiter()
                .GetResult();

            var props = Props.FromProducer(() => new MyActor());

            var config2 = ActorSystemConfig.Setup().WithMetrics();

            var remoteConfig2 = GrpcNetRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(MessagesReflection.Descriptor)
                .WithProtoMessages(SeedContractsReflection.Descriptor);

            var clusterConfig2 =
                ClusterConfig
                    .Setup("MyCluster", new SeedNodeClusterProvider(),
                        new PartitionIdentityLookup()
                    )
                    .WithClusterKind("somekind", props);

            var system2 = new ActorSystem(config2)
                .WithRemote(remoteConfig2)
                .WithCluster(clusterConfig2);

            Console.WriteLine($"System 2 Id {system2.Id}");

            var memberCluster2 = system2
                .Cluster();

            memberCluster2
                .StartMemberAsync()
                .GetAwaiter()
                .GetResult(); ;

            memberCluster2
                .JoinSeed(system.GetAddress())
                .GetAwaiter()
                .GetResult();

            _ = SafeTask.Run(async () => {
                    var r = new Random();

                    await Task.Delay(5000);

                    while (true)
                    {
                        await Task.Delay(r.Next(1, 2000));

                        var identity = $"someactor{r.Next(1, 100)}";

                        await system.Cluster().RequestAsync<SomeResponse>(identity, "somekind", new SomeRequest(),
                            CancellationTokens.FromSeconds(5)
                        );
                    }
                }
            );
        }
    }

    public record MyMessage(string Name);

    public class MyActor : IActor
    {
        private readonly Random r = new();

        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is SomeRequest m)
            {
                Console.WriteLine("tick..");
                await Task.Delay(r.Next(50, 500));
                context.Respond(new SomeResponse());
            }
        }
    }
}