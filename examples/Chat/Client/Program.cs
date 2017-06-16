using System;
using System.Threading.Tasks;
using chat.messages;
using Proto;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        Main2().GetAwaiter().GetResult();
    }
    
    public static async Task Main2()
    {
        Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
        Remote.Start("127.0.0.1", 0);
        var server = new PID("127.0.0.1:8000", "chatserver");

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
                case NickResponse nickResponse:
                    Console.WriteLine($"{nickResponse.OldUserName} is now {nickResponse.NewUserName}");
                    break;
            }
            return Actor.Done;
        });

        var client = Actor.Spawn(props);
        await server.SendAsync(new Connect
        {
            Sender = client
        });
        var nick = "Alex";
        while (true)
        {
            var text = Console.ReadLine();
            if (text.Equals("/exit"))
            {
                return;
            }
            if (text.StartsWith("/nick "))
            {
                var t = text.Split(' ')[1];
                await server.SendAsync(new NickRequest
                {
                    OldUserName = nick,
                    NewUserName = t
                });
                nick = t;
            }
            else
            {
                await server.SendAsync(new SayRequest
                {
                    UserName = nick,
                    Message = text
                });
            }
        }
    }
}
