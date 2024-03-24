// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using ClusterHelloWorld.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.CancellationTokens;
using ProtosReflection =ClusterHelloWorld.Messages.ProtosReflection;

class Program
{
    private static async Task Main()
    {
        Proto.Log.SetLoggerFactory(
            LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));
        
        // Required to allow unencrypted GrpcNet connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var system = new ActorSystem()
            .WithRemote(GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(ProtosReflection.Descriptor))
            .WithCluster(ClusterConfig
                .Setup("MyCluster", new SeedNodeClusterProvider(new(("127.0.0.1", 8090))), new PartitionIdentityLookup()));

        system.EventStream.Subscribe<ClusterTopology>(e => {
                Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}");
            }
        );
        
        await system
            .Cluster()
            .StartMemberAsync();

        Console.WriteLine("Started");


        var res = await system.Cluster().RequestAsync<HelloResponse>("123", "SomeKind", new HelloRequest()
            {
                EntityId = "456"
            }, FromSeconds(5));
        
        Console.WriteLine(res.Message);

        res = await system.Cluster().RequestAsync<HelloResponse>("123", "SomeKind", new HelloRequest()
        {
            EntityId = "678"
        }, FromSeconds(5));
        
        Console.WriteLine(res.Message);

        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
        Console.WriteLine("Shutting Down...");
        await system.Cluster().ShutdownAsync();
    }
}