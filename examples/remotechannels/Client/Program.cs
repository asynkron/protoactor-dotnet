using System;
using System.Threading.Channels;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static System.Threading.Channels.Channel;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;

var channel = CreateUnbounded<MyMessage>();

await StartClient(channel);

Console.WriteLine("Waiting for messages");
await foreach (var msg in channel.Reader.ReadAllAsync())
{
    Console.WriteLine($"Got message {msg.Value}");
}

static async Task StartClient(Channel<MyMessage> channel)
{
    var system =
        new ActorSystem()
            .WithRemote(BindToLocalhost());

    await system.Remote().StartAsync();

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
        }
    );

    system.Root.Spawn(props);
}
