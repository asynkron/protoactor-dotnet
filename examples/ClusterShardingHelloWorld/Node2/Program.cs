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
using Proto.Cluster.Sharding;
using Proto.Remote;
using Proto.Remote.GrpcNet;

using ProtosReflection = ClusterHelloWorld.Messages.ProtosReflection;

namespace Node2;

public class HelloEntity : IActor
{
    private readonly string _entityId;
    private readonly string _shardId;

    public HelloEntity(string shardId, string entityId)
    {
        _shardId = shardId;
        _entityId = entityId;
    }

    public Task ReceiveAsync(IContext context)
    {
        if (context.Message is HelloRequest hr)
        {
            return OnHelloRequest(hr, context);
        }

        return Task.CompletedTask;
    }

    private Task OnHelloRequest(HelloRequest request, IContext context)
    {
        Console.WriteLine("Got request!!");
        var res = new HelloResponse
        {
            Message = $"Hello from shard {_shardId} entity grain {_entityId}"
        };
        context.Respond(res);
        return Task.CompletedTask;
    }
}

class Program
{
    private static async Task Main()
    {
        Log.SetLoggerFactory(
            LoggerFactory.Create(l => l.AddConsole().SetMinimumLevel(LogLevel.Information)));
            
        // Required to allow unencrypted GrpcNet connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);

        var system = new ActorSystem(new ActorSystemConfig().WithDeveloperSupervisionLogging(true))
            .WithRemote(GrpcNetRemoteConfig.BindToLocalhost(8090).WithProtoMessages(ProtosReflection.Descriptor))
            .WithCluster(ClusterConfig
                .Setup("MyCluster", new SeedNodeClusterProvider(), new PartitionIdentityLookup())
                //TODO: this is not optimal as it creates a new props for each child
                .WithClusterKind("SomeKind", ShardActor.GetProps(((shardId, entityId, _) => Props.FromProducer(() => new HelloEntity(shardId, entityId))))
            ));
            
        system.EventStream.Subscribe<ClusterTopology>(e => {
                Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}");
            }
        );

        await system
            .Cluster()
            .StartMemberAsync();

        Console.WriteLine("Started...");

        Console.CancelKeyPress += async (e, y) => {
            Console.WriteLine("Shutting Down...");
            await system
                .Cluster()
                .ShutdownAsync();
        };


            
        await Task.Delay(-1);
    }
}