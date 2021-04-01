// -----------------------------------------------------------------------
// <copyright file="Foo.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Some.Namespace;
using Ubiquitous.Metrics.Prometheus;

namespace ActorMetrics
{
    public class RunDummyCluster
    {
        public static void Run()
        {
            ILoggerFactory l = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            ActorSystemConfig config = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());

            GrpcCoreRemoteConfig remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(MessagesReflection.Descriptor);

            ClusterConfig clusterConfig =
                ClusterConfig
                    .Setup("MyCluster",
                        new ConsulProvider(new ConsulProviderConfig(),
                            c => c.Address = new Uri("http://127.0.0.1:8500/")),
                        new PartitionIdentityLookup()
                    );

            ActorSystem system = new ActorSystem(config)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            system
                .Cluster()
                .StartMemberAsync();

            Props props = Props.FromProducer(() => new MyActor());

            ActorSystemConfig config2 = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());

            GrpcCoreRemoteConfig remoteConfig2 = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(MessagesReflection.Descriptor);

            ClusterConfig clusterConfig2 =
                ClusterConfig
                    .Setup("MyCluster",
                        new ConsulProvider(new ConsulProviderConfig(),
                            c => c.Address = new Uri("http://127.0.0.1:8500/")),
                        new PartitionIdentityLookup()
                    )
                    .WithClusterKind("somekind", props);

            ActorSystem system2 = new ActorSystem(config2)
                .WithRemote(remoteConfig2)
                .WithCluster(clusterConfig2);

            system2
                .Cluster()
                .StartMemberAsync();

            _ = SafeTask.Run(async () =>
                {
                    Random r = new Random();

                    while (true)
                    {
                        await Task.Delay(r.Next(1, 2000));
                        await system.Cluster().RequestAsync<SomeResponse>($"someactor{r.Next(1, 100)}", "somekind",
                            new SomeRequest(),
                            CancellationTokens.WithTimeout(5000)
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
