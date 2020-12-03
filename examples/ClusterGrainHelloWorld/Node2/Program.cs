// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request) =>
            Task.FromResult(new HelloResponse
                {
                    Message = "Hello from typed grain"
                }
            );
    }

    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup());

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            var grains = new Grains(system.Cluster());
            grains.HelloGrainFactory(() => new HelloGrain());

            Console.CancelKeyPress += async (e, y) =>
            {
                Console.WriteLine("Shutting Down...");
                await system
                    .Cluster()
                    .ShutdownAsync();
            };
            await Task.Delay(-1);
        }
    }
}