using System;
using Proto;

namespace EventStream
{
    //define some form of message
    public record SomeMessage(string Name);

    class Program
    {
        private static void Main()
        {
            var system = new ActorSystem();

            //subscribe to the eventstream via type
            system.EventStream.Subscribe<SomeMessage>(x => Console.WriteLine($"Got message for {x.Name}"));

            //publish messages onto the eventstream
            system.EventStream.Publish(new SomeMessage("ProtoActor"));
            system.EventStream.Publish(new SomeMessage("Asynkron"));

            //this example is local only.
            //see ClusterEventStream for cluster broadcast onto the eventstream

            Console.ReadLine();
        }
    }
}