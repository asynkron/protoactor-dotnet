using System;
using Messages;
using Proto;
using Proto.Channels;
using Proto.Remote.GrpcNet;
using static System.Threading.Channels.Channel;
using static Proto.Remote.GrpcNet.GrpcNetRemoteConfig;

var system = await ActorSystem.StartNew(Remote.Config(BindToLocalhost()));
var publisher = PID.FromAddress("127.0.0.1:8000", "publisher");
var channel = CreateUnbounded<MyMessage>();
_ = ChannelSubscriber.StartNew(system.Root, publisher, channel);

Console.WriteLine("Waiting for messages");
await foreach (var msg in channel.Reader.ReadAllAsync())
{
    Console.WriteLine($"Got message {msg.Value}");
}