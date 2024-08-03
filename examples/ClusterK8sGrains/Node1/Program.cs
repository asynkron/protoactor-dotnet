// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using ClusterHelloWorld.Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.PartitionActivator;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.CancellationTokens;
using ProtosReflection = ClusterHelloWorld.Messages.ProtosReflection;
using System.Runtime.Loader;
using Microsoft.Extensions.Configuration;
using Extensions = Proto.Remote.GrpcNet.Extensions;

// Hook SIGTERM to a cancel token to know when k8s is shutting us down
// hostBuilder should be used in production
var cts = new CancellationTokenSource();
AssemblyLoadContext.Default.Unloading += ctx => cts.Cancel();

Log.SetLoggerFactory(
    LoggerFactory.Create(l => l.AddConsole(options =>
        {
            //options.FormatterName = "json"; // Use the JSON formatter
        }).SetMinimumLevel(LogLevel.Debug)
        .AddFilter("Proto.Cluster.Gossip", LogLevel.Information)
        .AddFilter("Proto.Context.ActorContext", LogLevel.Information)));

// Required to allow unencrypted GrpcNet connections
AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

var kubernetesProvider = new KubernetesProvider();
var advertisedHost = await kubernetesProvider.GetPodFqdn();

var system = new ActorSystem()
    .WithRemote(GrpcNetRemoteConfig
        .BindToAllInterfaces(advertisedHost: advertisedHost, port: 4020)
        .WithProtoMessages(ProtosReflection.Descriptor))
    .WithCluster(ClusterConfig
        .Setup("MyCluster",
            kubernetesProvider,
            new PartitionActivatorLookup())
    );

system.EventStream.Subscribe<ClusterTopology>(
    e => { Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}"); }
);

await system
    .Cluster()
    .StartMemberAsync();

Console.WriteLine("Started");

try
{
    var helloGrain = system.Cluster().GetHelloGrain("MyGrain");

    var res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(15));
    Console.WriteLine(res?.Message ?? "RES IS NULL");

    res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
    Console.WriteLine(res?.Message ?? "RES IS NULL");
}
catch (Exception e)
{
    Log.CreateLogger("Program").LogError(e, "Error sending messages");
}

Console.WriteLine("Press CTRL-C to exit");
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent the process from terminating.
    cts.Cancel();
};

await Task.Delay(Timeout.Infinite, cts.Token);

Console.WriteLine("Shutting Down...");
await system.Cluster().ShutdownAsync();