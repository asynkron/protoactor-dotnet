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
using Proto.Context;
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
        var logger = Log.CreateLogger("benchmark");
        
        // Required to allow unencrypted GrpcNet connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var system = new ActorSystem(new ActorSystemConfig()
                .WithDeveloperSupervisionLogging(true)
                .WithDeadLetterRequestLogging(true)
                .WithDeadLetterResponseLogging(true)
                .WithConfigureProps(p => p.WithDeadlineDecorator(TimeSpan.FromSeconds(1), logger).WithLoggingContextDecorator(logger)))
            
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

        var helloGrain = system.Cluster().GetHelloGrain("MyGrain");
        
        var res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
        Console.WriteLine(res.Message);

        res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
        Console.WriteLine(res.Message);

        Console.WriteLine("Press enter to exit");
        Console.ReadLine();
        Console.WriteLine("Shutting Down...");
        await system.Cluster().ShutdownAsync();
    }
}