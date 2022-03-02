// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.IO.Compression;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2;

public class EchoActor : IActor
{
    private PID _sender;
    private static readonly Pong Pong = new Pong();
    // private int _count = 0;

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case StartRemote sr:
                // Console.WriteLine($"Starting for {sr.Sender}");
                _sender = sr.Sender;
                context.Respond(new Start());
                return Task.CompletedTask;
            case Ping _:
                context.Send(_sender, Pong);
                // if (++_count % 500_000 == 0)
                // {
                //     Console.WriteLine($"{_count} to {_sender}");
                // }
                return Task.CompletedTask;
            default:
                return Task.CompletedTask;
        }
    }
}

class Program
{
    private static async Task Main()
    {
        Log.SetLoggerFactory(LoggerFactory.Create(c => c
                .SetMinimumLevel(LogLevel.Information)
                .AddFilter("Microsoft", LogLevel.None)
                .AddFilter("Grpc", LogLevel.None)
                .AddConsole()
            )
        );

#if NETCOREAPP3_1
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif


        Console.WriteLine("Enter Advertised Host (Default = 127.0.0.1)");
        var advertisedHost = Console.ReadLine().Trim();
        if (string.IsNullOrEmpty(advertisedHost))
            advertisedHost = "127.0.0.1";

        var actorSystemConfig = new ActorSystemConfig()
            .WithDeadLetterThrottleCount(10)
            .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(2));
        var system = new ActorSystem(actorSystemConfig);
        var context = new RootContext(system);
        IRemote remote;
            
        var remoteConfig = GrpcNetRemoteConfig
            .BindTo(advertisedHost, 12000)
            .WithChannelOptions(new GrpcChannelOptions
                {
                    CompressionProviders = new ICompressionProvider[]
                    {
                        new GzipCompressionProvider(CompressionLevel.Fastest)
                    }
                }
            )
            .WithEndpointWriterMaxRetries(3)
            .WithProtoMessages(ProtosReflection.Descriptor)
            .WithRemoteKind("echo", Props.FromProducer(() => new EchoActor()));
        remote = new GrpcNetRemote(system, remoteConfig);

        await remote.StartAsync();
        context.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
        Console.ReadLine();
        await remote.ShutdownAsync();
    }
}