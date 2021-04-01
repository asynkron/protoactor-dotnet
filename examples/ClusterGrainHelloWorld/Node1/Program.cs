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

internal class Program
{
    private static async Task Main(string[] args)
    {
        GrpcCoreRemoteConfig remoteConfig = GrpcCoreRemoteConfig
            .BindToLocalhost()
            .WithProtoMessages(ProtosReflection.Descriptor);

        ConsulProvider consulProvider =
            new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

        ClusterConfig clusterConfig =
            ClusterConfig
                .Setup("MyCluster", consulProvider, new PartitionIdentityLookup());

        ActorSystem system = new ActorSystem()
            .WithRemote(remoteConfig)
            .WithCluster(clusterConfig);

        await system
            .Cluster()
            .StartMemberAsync();

        await Task.Delay(2000);

        Grains grains = new Grains(system.Cluster());
        HelloGrainClient client = grains.HelloGrain("Roger");

        HelloResponse res = await client.SayHello(new HelloRequest());
        Console.WriteLine(res.Message);

        res = await client.SayHello(new HelloRequest());
        Console.WriteLine(res.Message);
        Console.CancelKeyPress += async (e, y) =>
        {
            Console.WriteLine("Shutting Down...");
            await system.Cluster().ShutdownAsync();
        };
        await Task.Delay(-1);
    }
}
