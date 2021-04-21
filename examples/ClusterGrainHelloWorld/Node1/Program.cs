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
using static Proto.CancellationTokens;
using ProtosReflection =ClusterHelloWorld.Messages.ProtosReflection;

class Program
{
    private static async Task Main()
    {
        var system = new ActorSystem()
            .WithRemote(GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ProtosReflection.Descriptor))
            .WithCluster(ClusterConfig
                .Setup("MyCluster", new ConsulProvider(new ConsulProviderConfig()), new PartitionIdentityLookup()));

        await system
            .Cluster()
            .StartClientAsync();

        Console.WriteLine("Started");
        await Task.Delay(2000);


        var helloGrain = system.Cluster().GetHelloGrain("MyGrain");
        
        var res = await helloGrain.SayHello(new HelloRequest(), WithTimeout(5000));
        Console.WriteLine(res.Message);

        res = await helloGrain.SayHello(new HelloRequest(), WithTimeout(5000));
        Console.WriteLine(res.Message);
        
        Console.CancelKeyPress += async (e, y) => {
            Console.WriteLine("Shutting Down...");
            await system.Cluster().ShutdownAsync();
        };
        
        await Task.Delay(-1);
    }
}