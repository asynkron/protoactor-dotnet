// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using chat.messages;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;

namespace Server
{
    static class Program
    {
        private static RootContext context;

        private static void Main()
        {
            Log.SetLoggerFactory(LoggerFactory.Create(c => c
                .SetMinimumLevel(LogLevel.Information)
                .AddConsole()
            ));
            InitializeActorSystem();
            SpawnServer();

            Console.ReadLine();
            context.System.Remote().ShutdownAsync().GetAwaiter().GetResult();
        }

        private static void InitializeActorSystem()
        {
            var config =
                BindToLocalhost(8000)
                    .WithProtoMessages(ChatReflection.Descriptor)
                    .WithRemoteDiagnostics(true);

            var system
                = new ActorSystem()
                    .WithRemote(config);

            system
                .Remote()
                .StartAsync();

            context = system.Root;
        }

        private static void SpawnServer()
        {
            var clients = new HashSet<PID>();

            context.SpawnNamed(
                Props.FromFunc(
                    ctx => {
                        switch (ctx.Message)
                        {
                            case Connect connect:
                                Console.WriteLine($"Client {connect.Sender} connected");

                                foreach (var client in clients)
                                {
                                    ctx.Send(
                                        client,
                                        new SayResponse
                                        {
                                            UserName = "Server",
                                            Message = $"{connect.Sender} connected"
                                        }
                                    );
                                }

                                clients.Add(connect.Sender);

                                ctx.Send(
                                    connect.Sender,
                                    new Connected
                                    {
                                        Message = "Welcome!"
                                    }
                                );
                                break;
                            case SayRequest sayRequest:
                                foreach (var client in clients)
                                {
                                    ctx.Send(
                                        client,
                                        new SayResponse
                                        {
                                            UserName = sayRequest.UserName,
                                            Message = sayRequest.Message
                                        }
                                    );
                                }

                                break;
                            case NickRequest nickRequest:
                                foreach (var client in clients)
                                {
                                    ctx.Send(
                                        client,
                                        new NickResponse
                                        {
                                            OldUserName = nickRequest.OldUserName,
                                            NewUserName = nickRequest.NewUserName
                                        }
                                    );
                                }

                                break;
                        }

                        return Task.CompletedTask;
                    }
                ),
                "chatserver"
            );
        }
    }
}