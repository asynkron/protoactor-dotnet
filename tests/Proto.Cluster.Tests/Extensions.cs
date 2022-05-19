using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Proto.Cluster.Gossip;
using Xunit.Abstractions;

namespace Proto.Cluster.Tests;

public static class Extensions
{
    public static Task<Pong> Ping(
        this Cluster cluster,
        string id,
        string message,
        CancellationToken token,
        string kind = EchoActor.Kind
    )
        => cluster.RequestAsync<Pong>(id, kind, new Ping {Message = message}, token);
    
    
    public static async Task DumpClusterState(this IEnumerable<Cluster> members, ITestOutputHelper outputHelper)
    {
        foreach (var c in members)
        {
            var topology = await c.Gossip.GetState<ClusterTopology>(GossipKeys.Topology);
            outputHelper.WriteLine("Member " + c.System.Id);

            foreach (var kvp in topology)
            {
                outputHelper.WriteLine("\tData {0} - {1}", kvp.Key, kvp.Value.TopologyHash);
            }
        }
    }
}