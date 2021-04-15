// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading.Tasks;
using Cluster.HelloWorld.Messages;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using static System.Threading.Tasks.Task;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request) =>
            FromResult(new HelloResponse
                {
                    Message = "Hello from typed grain"
                }
            );
    }

    class Program
    {
        private static async Task Main()
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup())
                    .WithClusterKinds(Grains.GetClusterKinds());

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            Grains.
            grains.HelloGrainFactory(() => new HelloGrain());

            Console.CancelKeyPress += async (e, y) => {
                Console.WriteLine("Shutting Down...");
                await system
                    .Cluster()
                    .ShutdownAsync();
            };
            await Delay(-1);
        }
    }
}