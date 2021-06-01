using System;
using System.Threading.Tasks;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using ProtosReflection = Proto.ProtosReflection;

namespace EventStreamTopicsCluster
{
    public record SomeMessage(string Name, string Topic) : ITopicMessage;

    public interface ITopicMessage
    {
        string Topic { get; }
    }

    class Program
    {
        private static async Task Main()
        {
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(ProtosReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig(), c => c.Address = new Uri("http://consul:8500/"));

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup());

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            //subscribe to the eventstream via type, just like you do locally
            system.EventStream.SubscribeToTopic<SomeMessage>("MyTopic.*", x => Console.WriteLine($"Got message for {x.Name}"));

            //publish messages onto the eventstream on Subtopic1 on MyTopic root
            //but do this using cluster broadcasting. this will publish the event
            //to the event stream on all the members in the cluster
            //this is best effort only, if some member is unavailable, there is no guarantee associated here
            system.Cluster().BroadcastEvent(new SomeMessage("ProtoActor", "MyTopic.Subtopic1"));

            //this message is published on a topic that is not subscribed to, and nothing will happen
            system.Cluster().BroadcastEvent(new SomeMessage("Asynkron", "AnotherTopic"));

            //send a message to the same root topic, but another child topic
            system.Cluster().BroadcastEvent(new SomeMessage("Do we get this?", "MyTopic.Subtopic1"));

            //this example is local only.
            //see ClusterEventStream for cluster broadcast onto the eventstream

            Console.ReadLine();
        }
    }

    public static class Extensions
    {
        public static void BroadcastEvent<T>(this Cluster self, T message) => self.MemberList.BroadcastEvent(message);

        //use regex or whatever fits your needs for subscription to topic matching
        //here we use the built in Like operator from VB.NET for this. just as an example
        public static EventStreamSubscription<object> SubscribeToTopic<T>(this EventStream self, string topic, Action<T> body) where T : ITopicMessage
            => self.Subscribe<T>(x => {
                    if (!LikeOperator.LikeString(x.Topic, topic, CompareMethod.Binary))
                        return;

                    body(x);
                }
            );
    }
}