using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.Remote;
using Proto.Remote.GrpcCore;


namespace EventStreamTopicsCluster
{
    public partial class MyMessage : ITopicMessage
    {
        
    }

    public interface ITopicMessage
    {
        string Topic { get; }
    }

    class Program
    {
        private static async Task Main()
        {
            //Configure ProtoActor to use Serilog
            var l = LoggerFactory.Create(x => x.AddConsole().SetMinimumLevel(LogLevel.Information));
            Log.SetLoggerFactory(l);
            
            var remoteConfig = GrpcCoreRemoteConfig
                .BindToLocalhost()
                .WithProtoMessages(MessagesReflection.Descriptor);

            var consulProvider =
                new ConsulProvider(new ConsulProviderConfig());

            var clusterConfig =
                ClusterConfig
                    .Setup("MyCluster", consulProvider, new PartitionIdentityLookup());

            var system = new ActorSystem()
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig);

            await system
                .Cluster()
                .StartMemberAsync();

            Console.WriteLine("Started");

            //subscribe to the eventstream via type, just like you do locally
            system.EventStream.SubscribeToTopic<MyMessage>("MyTopic.*", x => Console.WriteLine($"Got message for {x.Name}"));

            //publish messages onto the eventstream on Subtopic1 on MyTopic root
            //but do this using cluster broadcasting. this will publish the event
            //to the event stream on all the members in the cluster
            //this is best effort only, if some member is unavailable, there is no guarantee associated here
            system.Cluster().BroadcastEvent(new MyMessage {
                Name = "ProtoActor", 
                Topic = "MyTopic.Subtopic1"
            });

            //this message is published on a topic that is not subscribed to, and nothing will happen
            system.Cluster().BroadcastEvent(new MyMessage {
                Name = "Asynkron", 
                Topic = "AnotherTopic"
            });

            //send a message to the same root topic, but another child topic
            system.Cluster().BroadcastEvent(new MyMessage
                {
                    Name = "Do we get this?",
                    Topic = "MyTopic.Subtopic1"
                }
            );

            //this example is local only.
            //see ClusterEventStream for cluster broadcast onto the eventstream

            Console.WriteLine("Done");
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