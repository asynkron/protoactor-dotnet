using System;
using System.Collections.Generic;
using chat.messages;
using Proto;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        Serialization.RegisterFileDescriptor(ChatReflection.Descriptor);
        Remote.Start("127.0.0.1", 8000);
        var clients = new HashSet<PID>();
        var props = Actor.FromFunc(async ctx =>
        {
            switch (ctx.Message)
            {
                case Connect connect:
                    Console.WriteLine($"Client {connect.Sender} connected");
                    clients.Add(connect.Sender);
                    await connect.Sender.SendAsync(new Connected { Message = "Welcome!"});
                    break;
                case SayRequest sayRequest:
                    foreach (var client in clients)
                    {
                        await client.SendAsync(new SayResponse
                        {
                            UserName = sayRequest.UserName,
                            Message = sayRequest.Message
                        });        
                    }
                    break;
                case NickRequest nickRequest:
                    foreach (var client in clients)
                    {
                        await client.SendAsync(new NickResponse
                        {
                            OldUserName = nickRequest.OldUserName,
                            NewUserName = nickRequest.NewUserName
                        });
                    }
                    break;
            }
        });
        Actor.SpawnNamed(props, "chatserver");
        Console.ReadLine();
    }
}