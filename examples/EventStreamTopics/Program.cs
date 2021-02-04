using System;
using Proto;

namespace EventStreamTopics
{
    public record SomeMessage(string Name, string Topic) : ITopicMessage ;

    public interface ITopicMessage
    {
        string Topic { get; }
    }
    
    class Program
    {
        static void Main(string[] args)
        {
            var system = new ActorSystem();
            
            //subscribe to the eventstream via type
            system.EventStream.SubscribeToTopic<SomeMessage>("MyTopic",x => Console.WriteLine($"Got message for {x.Name}"));
            
            //publish messages onto the eventstream
            system.EventStream.Publish(new SomeMessage("ProtoActor","MyTopic"));
            
            //this message is published on a topic that is not subscribed to, and nothing will happen
            system.EventStream.Publish(new SomeMessage("Asynkron", "AnotherTopic"));

            //this example is local only.
            //see ClusterEventStream for cluster broadcast onto the eventstream
            
            Console.ReadLine();
        }
    }

    public static class Extensions
    {
        public static EventStreamSubscription<object> SubscribeToTopic<T>(this EventStream self, string topic, Action<T> body) where T:ITopicMessage => self.Subscribe<T>(x => {
                if (x.Topic != topic) return;

                body(x);
            }
        );
    }
}