using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Channels;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using static System.Threading.Channels.Channel;

var system = new ActorSystem().WithRemote(BindToLocalhost(8000));
await system.Remote().StartAsync();

var channel = CreateUnbounded<MyMessage>();
ChannelPublisherActor<MyMessage>.StartNew(system.Root, channel, "publisher");

//produce messages
var i = 0;
while (true)
{
    Console.WriteLine("Sending message " + i);
    await channel.Writer.WriteAsync(new MyMessage(i));
    i++;
    await Task.Delay(1000);
}