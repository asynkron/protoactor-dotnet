// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using ClusterHelloWorld.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using static System.Threading.Tasks.Task;
using ProtosReflection =ClusterHelloWorld.Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : HelloGrainBase
    {
        private readonly string _identity;

        public HelloGrain(IContext ctx) : base(ctx) => _identity = Context.Get<ClusterIdentity>()!.Identity;

        public override Task<HelloResponse> SayHello(HelloRequest request) {
            var res = new HelloResponse
            {
                Message = $"Hello from typed grain {_identity}"
            };

            return FromResult(res);
        }
    }

    class Program
    {
        private static async Task Main()
        {
            //bind this interface to our concrete implementation
            Grains.Factory<HelloGrainBase>.Create = (ctx, _, _) => new HelloGrain(ctx);

            var system = new ActorSystem()
                .WithRemote(GrpcCoreRemoteConfig
                    .BindToLocalhost()
                    .WithProtoMessages(ProtosReflection.Descriptor)
                )
                .WithCluster(ClusterConfig
                    .Setup("MyCluster", new ConsulProvider(new ConsulProviderConfig()), new PartitionIdentityLookup())
                    .WithHelloHelloWorldKinds()
                );

            await system
                .Cluster()
                .StartMemberAsync();

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