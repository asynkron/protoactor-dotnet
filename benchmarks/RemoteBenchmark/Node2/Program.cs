// -----------------------------------------------------------------------
//  <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
//  </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Proto.Remote.GrpcNet;
using ProtosReflection = Messages.ProtosReflection;

namespace Node2
{
    public class EchoActor : IActor
    {
        private PID _sender;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case StartRemote sr:
                    Console.WriteLine("Starting");
                    _sender = sr.Sender;
                    context.Respond(new Start());
                    return Task.CompletedTask;
                case Ping _:
                    context.Send(_sender, new Pong());
                    return Task.CompletedTask;
                default:
                    return Task.CompletedTask;
            }
        }
    }

    class Program
    {
        static async Task Main(string[] args)
        {
            Log.SetLoggerFactory(LoggerFactory.Create(c => c
            .SetMinimumLevel(LogLevel.Information)
            .AddFilter("Proto.EventStream", LogLevel.None)
            .AddConsole()));

#if NETCORE
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif

            Console.WriteLine("Enter 0 to use GrpcCore provider");
            Console.WriteLine("Enter 1 to use GrpcNet provider");
            if (!int.TryParse(Console.ReadLine(), out var provider))
                provider = 0;

            var system = new ActorSystem();
            var context = new RootContext(system);
            IRemote remote;
            if (provider == 0)
            {
                var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost(12000)
                .WithEndpointWriterMaxRetries(0)
                .WithProtoMessages(ProtosReflection.Descriptor)
                .WithRemoteKind("echo", Props.FromProducer(() => new EchoActor()));
                remote = new GrpcCoreRemote(system, remoteConfig);
            }
            else
            {
                var remoteConfig = GrpcNetRemoteConfig
                .BindToLocalhost(12000)
                .WithEndpointWriterMaxRetries(0)
                .WithProtoMessages(ProtosReflection.Descriptor)
                .WithRemoteKind("echo", Props.FromProducer(() => new EchoActor()));
                remote = new GrpcNetRemote(system, remoteConfig);
            }
            await remote.StartAsync();
            context.SpawnNamed(Props.FromProducer(() => new EchoActor()), "remote");
            Console.ReadLine();
            await remote.ShutdownAsync();
        }
    }
}