using System;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Proto;

namespace EventStreamTopics;

public record SomeMessage(string Name, string Topic) : ITopicMessage;

public interface ITopicMessage
{
    string Topic { get; }
}

internal class Program
{
    private static void Main()
    {
        var system = new ActorSystem();

        //subscribe to the eventstream via type
        system.EventStream.SubscribeToTopic<SomeMessage>("MyTopic.*",
            x => Console.WriteLine($"Got message for {x.Name}"));

        //publish messages onto the eventstream on Subtopic1 on MyTopic root
        system.EventStream.Publish(new SomeMessage("ProtoActor", "MyTopic.Subtopic1"));

        //this message is published on a topic that is not subscribed to, and nothing will happen
        system.EventStream.Publish(new SomeMessage("Asynkron", "AnotherTopic"));

        //send a message to the same root topic, but another child topic
        system.EventStream.Publish(new SomeMessage("Do we get this?", "MyTopic.Subtopic1"));

        //this example is local only.
        //see ClusterEventStream for cluster broadcast onto the eventstream

        Console.ReadLine();
    }
}

public static class Extensions
{
    //use regex or whatever fits your needs for subscription to topic matching
    //here we use the built in Like operator from VB.NET for this. just as an example
    public static EventStreamSubscription<object> SubscribeToTopic<T>(this EventStream self, string topic,
        Action<T> body) where T : ITopicMessage =>
        self.Subscribe<T>(x =>
            {
                if (!LikeOperator.LikeString(x.Topic, topic, CompareMethod.Binary))
                {
                    return;
                }

                body(x);
            }
        );
}