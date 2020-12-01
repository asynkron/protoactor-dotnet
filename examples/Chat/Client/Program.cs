// -----------------------------------------------------------------------
// <copyright file="Program.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using chat.messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using static Proto.Remote.GrpcCore.GrpcCoreRemoteConfig;

namespace Client
{
    internal static class Program
    {
        private static void Main()
        {
            var config =
                BindToLocalhost()
                    .WithProtoMessages(ChatReflection.Descriptor);

            var system = new ActorSystem()
                .WithRemote(config);

            system
                .Remote()
                .StartAsync();

            var server = PID.FromAddress("127.0.0.1:8000", "chatserver");
            var context = system.Root;

            var props = Props.FromFunc(
                ctx =>
                {
                    switch (ctx.Message)
                    {
                        case Connected connected:
                            Console.WriteLine(connected.Message);
                            break;
                        case SayResponse sayResponse:
                            Console.WriteLine($"{sayResponse.UserName} {sayResponse.Message}");
                            break;
                        case NickResponse nickResponse:
                            Console.WriteLine($"{nickResponse.OldUserName} is now {nickResponse.NewUserName}");
                            break;
                    }

                    return Task.CompletedTask;
                }
            );

            var client = context.Spawn(props);

            context.Send(
                server, new Connect
                {
                    Sender = client
                }
            );
            var nick = "Alex";

            while (true)
            {
                var text = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                if (text.Equals("/exit"))
                    return;

                if (text.StartsWith("/nick "))
                {
                    var t = text.Split(' ')[1];

                    context.Send(
                        server, new NickRequest
                        {
                            OldUserName = nick,
                            NewUserName = t
                        }
                    );
                    nick = t;
                }
                else
                {
                    context.Send(
                        server, new SayRequest
                        {
                            UserName = nick,
                            Message = text
                        }
                    );
                }
            }
        }
    }
}