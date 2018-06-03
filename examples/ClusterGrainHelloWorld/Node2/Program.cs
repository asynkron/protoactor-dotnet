// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
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
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

            Grains.HelloGrainFactory(() => new HelloGrain());

            Cluster.Start("MyCluster", "127.0.0.1", 12000, new ConsulProvider(new ConsulProviderOptions()));
            Console.ReadLine();
            Console.WriteLine("Shutting Down...");
            Cluster.Shutdown();
        }
    }
}