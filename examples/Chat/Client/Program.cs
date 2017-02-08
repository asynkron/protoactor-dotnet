using System;
using chat.messages;
using Proto;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
        RemotingSystem.Start("127.0.0.1", 12001);
        var server = new PID("127.0.0.1:8080", "chatserver");

        var props = Actor.FromFunc(ctx =>
        {
            switch (ctx.Message)
            {
                case Connected connected:
                    Console.WriteLine(connected.Message);
                    break;
                case SayResponse sayResponse:
                    Console.WriteLine($"{sayResponse.UserName} {sayResponse.Message}");
                    break;
                case NickResponse nickResponse :
                    Console.WriteLine($"{nickResponse.OldUserName} is now {nickResponse.NewUserName}");
                    break;
            }
            return Actor.Done;
        });

        var client = Actor.Spawn(props);
        server.Tell(new Connect
        {
            Sender = client
        });
        var nick = "Roger";
        server.Tell(new SayRequest
        {
            UserName = nick,
            Message = "text"
        });
        server.Tell(new NickRequest
        {
            OldUserName = nick,
            NewUserName = "Prem"
        });
        Console.ReadLine();
    }
}