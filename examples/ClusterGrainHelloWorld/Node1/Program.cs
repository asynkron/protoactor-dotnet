﻿// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using ClusterHelloWorld.Messages;
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
        // Required to allow unencrypted GrpcNet connections
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
        var system = new ActorSystem()
            .WithRemote(GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(ProtosReflection.Descriptor))
            .WithCluster(ClusterConfig
                .Setup("MyCluster", new SeedNodeClusterProvider(), new PartitionIdentityLookup()));

        system.EventStream.Subscribe<ClusterTopology>(e => {
                Console.WriteLine($"{DateTime.Now:O} My members {e.TopologyHash}");
            }
        );
        
        await system
            .Cluster()
            .StartMemberAsync();

        await system.Cluster().JoinSeedNode("127.0.0.1", 8090);

        Console.WriteLine("Started");
        await Task.Delay(2000);


        var helloGrain = system.Cluster().GetHelloGrain("MyGrain");
        
        var res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
        Console.WriteLine(res.Message);

        res = await helloGrain.SayHello(new HelloRequest(), FromSeconds(5));
        Console.WriteLine(res.Message);

        Console.CancelKeyPress += async (e, y) => {
            Console.WriteLine("Shutting Down...");
            await system.Cluster().ShutdownAsync();
        };


        
        await Task.Delay(-1);
    }
}