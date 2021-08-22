using System;
using System.Collections.Generic;
using System.Threading.Channels;
using System.Threading.Tasks;
using Messages;
using Proto;
using Proto.Channels;
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
    
    ChannelReaderActor<MyMessage>.StartNew(system.Root, channel);
}
