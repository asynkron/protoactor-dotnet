// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Cluster.HelloWorld.Messages;
using Messages;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using static Proto.CancellationTokens;
using ProtosReflection = Messages.ProtosReflection;

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
                .Setup("MyCluster", consulProvider, new PartitionIdentityLookup());

        var system = new ActorSystem()
            .WithRemote(remoteConfig)
            .WithCluster(clusterConfig);

        await system
            .Cluster()
            .StartMemberAsync();

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