// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
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
using static Proto.Remote.RemoteConfig;

Log.SetLoggerFactory(LoggerFactory.Create(c => c
    .SetMinimumLevel(LogLevel.Information)
    .AddConsole()
));

var config =
    BindToLocalhost(8000)
        .WithProtoMessages(ChatReflection.Descriptor)
        .WithRemoteDiagnostics(true);

var system = new ActorSystem().WithRemote(config);
await system.Remote().StartAsync();

var context = system.Root;
var clients = new HashSet<PID>();

context.SpawnNamed(
    Props.FromFunc(
        ctx =>
        {
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

Console.ReadLine();
await system.Remote().ShutdownAsync();
