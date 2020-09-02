using System;
using System.Collections.Generic;
using chat.messages;
using Proto;
using Proto.Remote;

namespace Server
{
    class Program
    {
        static void Main()
        {
            var system = new ActorSystem();
            var context = new RootContext(system);

            var remote = new SelfHostedRemote(system, 8000, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
            });
            remote.Start();

            var clients = new HashSet<PID>();

            var props = Props.FromFunc(
                ctx =>
                {
                    switch (ctx.Message)
                    {
                        case Connect connect:
                            Console.WriteLine($"Client {connect.Sender} connected");
                            clients.Add(connect.Sender);
                            ctx.Send(connect.Sender, new Connected { Message = "Welcome!" });
                            break;
                        case SayRequest sayRequest:
                            foreach (var client in clients)
                            {
                                ctx.Send(
                                    client, new SayResponse
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
                                    client, new NickResponse
                                    {
                                        OldUserName = nickRequest.OldUserName,
                                        NewUserName = nickRequest.NewUserName
                                    }
                                );
                            }

                            break;
                    }

                    return Actor.Done;
                }
            );

            context.SpawnNamed(props, "chatserver");
            Console.ReadLine();
        }
    }
}
