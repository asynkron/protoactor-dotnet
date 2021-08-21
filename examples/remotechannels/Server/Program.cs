using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using static System.Threading.Channels.Channel;

var channel = CreateUnbounded<MyMessage>();

await StartServer(channel);

//produce messages
var i = 0;
while (true)
{
    Console.WriteLine("Sending message " + i);
    await channel.Writer.WriteAsync(new MyMessage(i));
    i++;
    await Task.Delay(1000);
}

static async Task StartServer(Channel<MyMessage> channel)
{
    var system = new ActorSystem().WithRemote(BindToLocalhost(8000));
    await system.Remote().StartAsync();
    var subscribers = new HashSet<PID>();

    //define server actor
    var props = Props.FromFunc(ctx => {
            switch (ctx.Message)
            {
                case Subscribe:
                    subscribers.Add(ctx.Sender);
                    ctx.Respond(new Subscribed());
                    break;
                case MyMessage msg: {
                    foreach (var sub in subscribers)
                    {
                        ctx.Send(sub, msg);
                    }

                    break;
                }
            }

            return Task.CompletedTask;
        }
    );

    //spawn server actor
    var pid = system.Root.SpawnNamed(props, "server");

    //move messages from source channel to server actor
    _ = Task.Run(async () => {
            await foreach (var msg in channel.Reader.ReadAllAsync())
            {
                system.Root.Send(pid, msg);
            }
        }
    );
}
