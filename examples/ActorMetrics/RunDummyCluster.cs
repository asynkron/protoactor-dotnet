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
using Proto.Remote.GrpcCore;
using Ubiquitous.Metrics.Prometheus;

namespace ActorMetrics
{
    public class RunDummyCluster
    {
        public static void Run()
        {
            var l = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            var config = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());

            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost();

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://127.0.0.1:8500/")), new PartitionIdentityLookup());

            var system = new ActorSystem(config)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            system
                .Cluster()
                .StartMemberAsync();

            
            var config2 = ActorSystemConfig.Setup().WithMetricsProviders(new PrometheusConfigurator());

            var remoteConfig2 = GrpcCoreRemoteConfig
                .BindToLocalhost();

            var clusterConfig2 =
                ClusterConfig
                    .Setup("MyCluster", new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://127.0.0.1:8500/")), new PartitionIdentityLookup());

            
            var system2 = new ActorSystem(config2)
                .WithRemote(remoteConfig2)
                .WithCluster(clusterConfig2);
            
            system2
                .Cluster()
                .StartMemberAsync();
            
            var props = Props.FromProducer(() => new MyActor());

            var pid = system.Root.Spawn(props);
            system.Root.Send(pid, new MyMessage("Asynkron"));
          //  system.Root.Poison(pid);

            _ = SafeTask.Run(async () => {

                    var r = new Random();
                    while (true)
                    {
                        await Task.Delay(r.Next(1,2000));
                        system.Root.Send(pid, new MyMessage("Asynkron"));

                    }

                }
            );

        }
    }
    
    public record MyMessage(string Name);

    public class MyActor : IActor
    {
        private Random r = new();
        public async Task ReceiveAsync(IContext context)
        {
            if (context.Message is MyMessage m)
            {
                await Task.Delay(r.Next(50, 500));
                Console.WriteLine(m.Name);
            }
        }
    }
}