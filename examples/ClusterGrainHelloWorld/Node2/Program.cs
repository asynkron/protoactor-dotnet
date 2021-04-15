// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Cluster.HelloWorld.Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using static System.Threading.Tasks.Task;
using ProtosReflection =Cluster.HelloWorld.Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        private readonly IContext _ctx;
        private readonly string _identity;

        public HelloGrain(IContext ctx, string identity)
        {
            _ctx = ctx;
            _identity = identity;
        }

        public Task<HelloResponse> SayHello(HelloRequest request)
        {
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
            Grains.Factory<IHelloGrain>.Create = (ctx, identity, _) => new HelloGrain(ctx, identity);

            var system = new ActorSystem()
                .WithRemote(GrpcCoreRemoteConfig
                    .BindToLocalhost()
                    .WithProtoMessages(ProtosReflection.Descriptor)
                )
                .WithCluster(ClusterConfig
                    .Setup("MyCluster", new ConsulProvider(new ConsulProviderConfig()), new PartitionIdentityLookup())
                    .WithClusterKinds(Grains.GetClusterKinds())
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