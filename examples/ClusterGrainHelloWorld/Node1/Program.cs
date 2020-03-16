// -----------------------------------------------------------------------
//   <copyright file="Program.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;

class Program
{
    static async Task Main(string[] args)
    {
        var system = new ActorSystem();
        var serialization = new Serialization();
        var cluster = new Cluster(system, serialization);
        var grains = new Grains(cluster);
        serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);

        await cluster.Start("MyCluster", "node1", 12001, new ConsulProvider(new ConsulProviderOptions(), c => c.Address = new Uri("http://consul:8500/")));
        await Task.Delay(2000);

        var client = grains.HelloGrain("Roger");

        var res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);


        res = client.SayHello(new HelloRequest()).Result;
        Console.WriteLine(res.Message);
        Console.CancelKeyPress += async (e, y) =>
        {
            Console.WriteLine("Shutting Down...");
            await cluster.Shutdown();
        };
        await Task.Delay(-1);
    }
}