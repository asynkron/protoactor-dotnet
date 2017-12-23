// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    class Program
    {
        static void Main(string[] args)
        {
            Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            var props = Actor.FromFunc(ctx =>
            {
                switch (ctx.Message)
                {
                    case HelloRequest _:
                        ctx.Respond(new HelloResponse
                        {
                            Message = "Hello from node 2"
                        });
                        break;
                }
                return Actor.Done;
            });

            Remote.RegisterKnownKind("HelloKind", props);
            Cluster.Start("MyCluster", "node2", 12000, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/")));
            System.Threading.Thread.Sleep(System.Threading.Timeout.Infinite)
            Console.WriteLine("Shutting Down...");
            Cluster.Shutdown();
        }
    }
}