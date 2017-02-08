using System;
using System.Collections.Generic;
using Chat;
using Proto;
using Proto.Remote;

class Program
{
    static void Main(string[] args)
    {
        RemotingSystem.Start("127.0.0.1", 8080);
        var clients = new HashSet<PID>();
        var props = Actor.FromFunc(ctx =>
        {
            switch (ctx.Message)
            {
                case Connect connect:
                    Console.WriteLine($"Client {connect.Sender} connected");
                    clients.Add(connect.Sender);
                    connect.Sender.Tell(new Connected { Message = "Welcome!"});
                    break;
                case SayRequest sayRequest:
                    foreach (var client in clients)
                    {
                        client.Tell(new SayResponse
                        {
                            UserName = sayRequest.UserName,
                            Message = sayRequest.UserName
                        });        
                    }
                    break;
                case NickRequest nickRequest:
                    foreach (var client in clients)
                    {
                        client.Tell(new NickRequest
                        {
                            OldUserName = nickRequest.OldUserName,
                            NewUserName = nickRequest.NewUserName
                        });
                    }
                    break;
            }
            return Actor.Done;
        });
        Actor.SpawnNamed(props, "chatserver");
        Console.ReadLine();
    }
}