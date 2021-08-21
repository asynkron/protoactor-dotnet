using System;
using Messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static System.Threading.Channels.Channel;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;

var config =
    BindToLocalhost();

var system =
    new ActorSystem()
        .WithRemote(config);

await system.Remote().StartAsync();

var context = system.Root;

var channel = CreateUnbounded<MyMessage>();
var server = PID.FromAddress("127.0.0.1:8000", "server");
var props = Props.FromFunc(async ctx => {
    switch (ctx.Message)
    {
        case Started:
            ctx.Request(server, new Subscribe());
            break;
        case Subscribed:
            Console.WriteLine("Subscribed");
            break;
        case MyMessage msg:
            await channel.Writer.WriteAsync(msg);
            break;
    }
});
var pid = context.Spawn(props);


Console.WriteLine("Waiting for messages");
await foreach (var msg in channel.Reader.ReadAllAsync())
{
    Console.WriteLine($"Got message {msg.Value}");
}
