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
using Proto.Cluster.PartitionActivator;
using Proto.Cluster.Seed;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.CancellationTokens;
using ProtosReflection = ClusterHelloWorld.Messages.ProtosReflection;

Log.SetLoggerFactory(
    LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));

// Required to allow unencrypted GrpcNet connections
// AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var system = new ActorSystem()
    .WithRemote(GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(ProtosReflection.Descriptor))
    .WithCluster(ClusterConfig
        .Setup("MyCluster",
            SeedNodeClusterProvider.JoinSeedNode("127.0.0.1",8090),
            new PartitionActivatorLookup()));

system.EventStream.Subscribe<ClusterTopology>(
    e => { Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}"); }
);

await system
    .Cluster()
    .StartMemberAsync();

Console.WriteLine("Started");


var helloGrain = system.Cluster().GetHelloGrain("MyGrain");

var res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(15));
Console.WriteLine(res.Message);

res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
Console.WriteLine(res.Message);

Console.WriteLine("Press enter to exit");
Console.ReadLine();
Console.WriteLine("Shutting Down...");
await system.Cluster().ShutdownAsync();