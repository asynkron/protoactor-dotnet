using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Proto.Remote;
using System.Linq;

namespace ClusterGossip;

internal static class Program
{
    private const string GossipKey = "demo-key";

    public static async Task Main()
    {
        Log.SetLoggerFactory(LoggerFactory.Create(b => b.AddConsole().SetMinimumLevel(LogLevel.Information)));

        var agent = new InMemAgent();

        var cluster1 = CreateCluster(agent);
        var cluster2 = CreateCluster(agent);

        await cluster1.StartMemberAsync();
        await cluster2.StartMemberAsync();

        await cluster1.Gossip.SetStateAsync(GossipKey, new StringValue { Value = "hello from node1" });

        await Task.Delay(1000);

        var values = await cluster2.Gossip.GetState<StringValue>(GossipKey);
        foreach (var (member, v) in values)
        {
            Console.WriteLine($"Node2 received '{v.Value}' from {member}");
        }

        var topology = await cluster2.Gossip.GetState<ClusterTopology>(GossipKeys.Topology);
        var first = topology.Values.FirstOrDefault();

        Console.WriteLine("Members seen via gossip:");
        if (first != null)
        {
            foreach (var member in first.Members)
            {
                Console.WriteLine($" - {member.Id}");
            }
        }

        Console.WriteLine("Press any key to exit");
        Console.ReadKey();

        await cluster1.ShutdownAsync();
        await cluster2.ShutdownAsync();
    }

    private static Cluster CreateCluster(InMemAgent agent)
    {
        var system = new ActorSystem()
            .WithRemote(RemoteConfig.BindToLocalhost().WithProtoMessages(WrappersReflection.Descriptor))
            .WithCluster(
                ClusterConfig.Setup("gossip-cluster", new TestProvider(new TestProviderOptions(), agent),
                        new PartitionIdentityLookup())
                    .WithClusterKind("empty", Props.FromProducer(() => new EmptyActor())));

        return system.Cluster();
    }

    private class EmptyActor : IActor
    {
        public Task ReceiveAsync(IContext context) => Task.CompletedTask;
    }
}
