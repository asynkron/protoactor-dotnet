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
                        return ctx.RespondAsync(new HelloResponse
                        {
                            Message = "Hello from node 2"
                        });
                }
                return Actor.Done;
            });

            Remote.RegisterKnownKind("HelloKind", props);
            Remote.Start("127.0.0.1", 12000);
            Cluster.Start("MyCluster", new ConsulProvider(new ConsulProviderOptions()));

            Console.ReadLine();
        }
    }
}