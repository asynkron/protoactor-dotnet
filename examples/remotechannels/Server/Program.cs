using System;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Channels;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;
using static System.Threading.Channels.Channel;
using static Proto.Remote.GrpcNet.Remote;

var system = await ActorSystem.StartNew(Config(BindToLocalhost(8000)));

var channel = CreateUnbounded<MyMessage>();
_ = ChannelPublisher.StartNew(system.Root, channel, "publisher");

//produce messages
for (var i = 0; i < 30; i++)
{
    Console.WriteLine("Sending message " + i);
    await channel.Writer.WriteAsync(new MyMessage(i));
    await Task.Delay(1000);
}