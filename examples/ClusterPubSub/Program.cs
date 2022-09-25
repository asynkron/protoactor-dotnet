using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Partition;
using Proto.Cluster.PubSub;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Proto.Timers;

namespace ClusterPubSub;

internal static class Program
{
    private static async Task Main()
    {
        Log.SetLoggerFactory(LoggerFactory.Create(l =>
                l.AddConsole().SetMinimumLevel(LogLevel.Information)
            )
        );

        var system = GetSystem();
        var cluster = system.Cluster();

        await cluster.StartMemberAsync();

        for (var i = 0; i < 3; i++)
        {
            await cluster.GetUserActor("user-" + i).Connect(CancellationToken.None);
        }

        Console.ReadKey();

        await cluster.ShutdownAsync();
    }

    private static ActorSystem GetSystem() =>
        new ActorSystem()
            .WithRemote(GetRemoteConfig())
            .WithCluster(GetClusterConfig());

    private static GrpcNetRemoteConfig GetRemoteConfig() =>
        GrpcNetRemoteConfig
            .BindToLocalhost()
            .WithProtoMessages(ProtosReflection.Descriptor);

    private static ClusterConfig GetClusterConfig()
    {
        var clusterConfig =
            ClusterConfig
                .Setup("MyCluster", new TestProvider(new TestProviderOptions(), new InMemAgent()),
                    new PartitionIdentityLookup())
                .WithClusterKind(UserActorActor.Kind,
                    Props.FromProducer(() => new UserActorActor((c, _) => new User(c))));

        return clusterConfig;
    }
}

public class User : UserActorBase
{
    private const string ChatTopic = "chat";

    private static readonly string[] _messages =
    {
        "Good day sir!",
        "Lovely weather, innit?",
        "How do you do?",
        "Pardon me!"
    };

    private CancellationTokenSource _schedule;

    public User(IContext context) : base(context)
    {
    }

    public override Task Connect()
    {
        _schedule = Context.Scheduler()
            .SendRepeatedly(
                TimeSpan.FromSeconds(new Random().Next(2, 5)),
                Context.Self,
                new Tick());

        return Context.Cluster().Subscribe(ChatTopic, Context.ClusterIdentity()!);
    }

    public override Task OnStopping()
    {
        _schedule.Cancel();

        return Context.Cluster().Unsubscribe(ChatTopic, Context.ClusterIdentity()!);
    }

    public override Task OnReceive()
    {
        switch (Context.Message)
        {
            case Tick:
                var message = _messages[new Random().Next(0, _messages.Length)];
                Console.WriteLine($"{Context.ClusterIdentity()!.Identity} publishes '{message}'");

                _ = Context.Cluster()
                    .Publisher()
                    .Publish(ChatTopic, new ChatMessage
                    {
                        Sender = Context.ClusterIdentity()!.Identity,
                        Message = message
                    });

                break;

            case ChatMessage msg:
                Console.WriteLine($"{Context.ClusterIdentity()!.Identity} received '{msg.Message}' from {msg.Sender}");

                break;
        }

        return Task.CompletedTask;
    }
}

internal record Tick;