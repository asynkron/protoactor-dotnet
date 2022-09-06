using System.Threading.Channels;
using Channels;
using Proto;

// This example shows how actors can be used to implement a simple pub-sub system.
// Messages sent to outChannel will be broadcast and received in inChannel1 and inChannel2

var outChannel = Channel.CreateUnbounded<string>();
var inChannel1 = Channel.CreateUnbounded<string>();
var inChannel2 = Channel.CreateUnbounded<string>();

var actorSystem = new ActorSystem(ActorSystemConfig.Setup());

var publisher = ChannelPublisher.StartNew(actorSystem.Root, outChannel, "publisher");
await ChannelSubscriber.StartNew(actorSystem.Root, publisher, inChannel1);
await ChannelSubscriber.StartNew(actorSystem.Root, publisher, inChannel2);

var messages = new[] { "Hello", "World", "!" };

foreach (var message in messages)
{
    await outChannel.Writer.WriteAsync(message);
}
outChannel.Writer.Complete();

var receivedMessages1 = new List<string>();
await foreach (var msg in inChannel1.Reader.ReadAllAsync())
{
    receivedMessages1.Add(msg);
}

var receivedMessages2 = new List<string>();
await foreach (var msg in inChannel2.Reader.ReadAllAsync())
{
    receivedMessages2.Add(msg);
}

Console.WriteLine("Received 1: " + string.Join(",", receivedMessages1));
Console.WriteLine("Received 2: " + string.Join(",", receivedMessages2));

await actorSystem.ShutdownAsync();