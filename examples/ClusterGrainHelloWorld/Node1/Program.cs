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
using Proto.Cluster.Partition;
using Proto.Remote;
using ProtosReflection = Messages.ProtosReflection;
using Proto.Remote.GrpcCore;

class Program
{
    static async Task Main(string[] args)
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
        
        var grains = new Grains(system.Cluster());
        var client = grains.HelloGrain("Roger");

        var res = await client.SayHello(new HelloRequest());
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