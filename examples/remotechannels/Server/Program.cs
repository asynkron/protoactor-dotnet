using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using static System.Threading.Channels.Channel;

var system = new ActorSystem().WithRemote(BindToLocalhost(8000));
await system.Remote().StartAsync();

var context = system.Root;
var channel = CreateUnbounded<MyMessage>();

var subscribers = new HashSet<PID>();

//define server actor
var props = Props.FromFunc( ctx => {
        switch (ctx.Message)
        {
            case Subscribe:
                subscribers.Add(ctx.Sender);
                ctx.Respond(new Subscribed());
                break;
            case MyMessage msg: {
                foreach (var sub in subscribers)
                {
                    ctx.Send(sub,msg);
                }

                break;
            }
        }

        return Task.CompletedTask;
    }
);

//spawn server actor
var pid = context.SpawnNamed(props, "server");

//move messages from source channel to server actor
_ = Task.Run(async () => {
    await foreach (var msg in channel.Reader.ReadAllAsync())
    {
        context.Send(pid, msg);
    } 
});


//produce messages
var i = 0;
while (true)
{
    Console.WriteLine("Sending message " + i);
    await channel.Writer.WriteAsync(new MyMessage(i));
    i++;
    await Task.Delay(1000);
}
