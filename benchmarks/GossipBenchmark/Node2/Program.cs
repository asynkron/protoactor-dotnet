﻿// -----------------------------------------------------------------------
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
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.Cluster.SeedNode.Redis;
using Proto.Context;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using StackExchange.Redis;
using static System.Threading.Tasks.Task;
using ProtosReflection = ClusterHelloWorld.Messages.ProtosReflection;

namespace Node2;

public class HelloGrain : HelloGrainBase
{
    private readonly string _identity;

    public HelloGrain(IContext ctx, string identity)
        : base(ctx) => _identity = identity;

    public override Task<HelloResponse> SayHello(HelloRequest request)
    {
        Console.WriteLine("Got request!!");
        var res = new HelloResponse { Message = $"Hello from typed grain {_identity}" };

        return FromResult(res);
    }
}

class Program
{
    private static async Task Main()
    {
        Proto.Log.SetLoggerFactory(
            LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information))
        );

        var logger = Log.CreateLogger("benchmark");

        // Required to allow unencrypted GrpcNet connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var system = new ActorSystem(
            new ActorSystemConfig()
                .WithDeveloperSupervisionLogging(true)
                .WithDeadLetterRequestLogging(true)
                .WithDeadLetterResponseLogging(true)
                .WithConfigureProps(
                    p =>
                        p.WithDeadlineDecorator(TimeSpan.FromSeconds(1), logger)
                            .WithLoggingContextDecorator(logger)
                )
        )
            .WithRemote(
                GrpcNetRemoteConfig
                    .BindToLocalhost(8090)
                    .WithProtoMessages(ProtosReflection.Descriptor)
            )
            .WithCluster(
                ClusterConfig
                    .Setup(
                        "MyCluster",
                        new ConsulProvider(new ConsulProviderConfig()),
                        new PartitionIdentityLookup()
                    )
                    .WithClusterKind(
                        HelloGrainActor.GetClusterKind(
                            (ctx, identity) => new HelloGrain(ctx, identity.Identity)
                        )
                    )
            );

        system.EventStream.Subscribe<ClusterTopology>(e =>
        {
            Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}");
        });

        await system.Cluster().StartMemberAsync();

        Console.WriteLine("Started...");

        Console.CancelKeyPress += async (e, y) =>
        {
            Console.WriteLine("Shutting Down...");
            await system.Cluster().ShutdownAsync();
        };

        await Delay(-1);
    }
}
