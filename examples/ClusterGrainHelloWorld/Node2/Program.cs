// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class HelloGrain : IHelloGrain
    {
        public Task<HelloResponse> SayHello(HelloRequest request)
        {
            return Task.FromResult(new HelloResponse
            {
                Message = "Hello from typed grain"
            });
        }
    }
    class Program
    {
        static async Task Main(string[] args)
        {
            var system = new ActorSystem();
            var serialization = new Serialization();
            var cluster = new Cluster(system, serialization);
            var grains = new Grains(cluster);
            serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            grains.HelloGrainFactory(() => new HelloGrain());

            await cluster.Start("MyCluster", "node2", 12000, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/")));

            Console.CancelKeyPress += async (e, y) =>
            {
                Console.WriteLine("Shutting Down...");
                await cluster.Shutdown();
            };
            await Task.Delay(-1);
        }
    }
}