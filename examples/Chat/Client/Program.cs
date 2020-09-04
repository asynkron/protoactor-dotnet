using System;
using chat.messages;
using Proto;
using Proto.Remote;

namespace Client
{
    static class Program
    {
        static void Main()
        {
            var system = new ActorSystem();
            var remote = new SelfHostedRemote(system, "127.0.0.1", 0, remote =>
            {
                remote.Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
            });
            remote.Start();
            var server = new PID("127.0.0.1:8000", "chatserver");
            var context = new RootContext(system);

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

                    return Actor.Done;
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
