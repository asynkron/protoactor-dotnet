// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Runtime.Loader;
using System.Threading.Tasks;
using System.Threading;
using ClusterHelloWorld.Messages;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.PartitionActivator;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static System.Threading.Tasks.Task;
using ProtosReflection = ClusterHelloWorld.Messages.ProtosReflection;

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

var system = new ActorSystem(new ActorSystemConfig().WithDeveloperSupervisionLogging(true))
    .WithRemote(GrpcNetRemoteConfig
        .BindToAllInterfaces(advertisedHost: advertisedHost, port: 4020)
        .WithProtoMessages(ProtosReflection.Descriptor))
    .WithCluster(ClusterConfig
        .Setup("MyCluster",
            kubernetesProvider,
            new PartitionActivatorLookup())
        .WithClusterKind(
            HelloGrainActor.GetClusterKind((ctx, identity) => new HelloGrain(ctx, identity.Identity)))
    );

system.EventStream.Subscribe<ClusterTopology>(
    e => { Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}"); }
);

await system
    .Cluster()
    .StartMemberAsync();

Console.WriteLine("Started...");

Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true; // prevent the process from terminating.
    cts.Cancel();
};

await Delay(Timeout.Infinite, cts.Token);
Console.WriteLine("Shutting Down...");

public class HelloGrain : HelloGrainBase
{
    private readonly string _identity;

    public HelloGrain(IContext ctx, string identity) : base(ctx)
    {
        _identity = identity;
    }

    public override Task<HelloResponse> SayHello(HelloRequest request)
    {
        Console.WriteLine("Got request!!");

        var res = new HelloResponse
        {
            Message = $"Hello from typed grain {_identity} | {DateTime.UtcNow:O}"
        };

        return FromResult(res);
    }
}