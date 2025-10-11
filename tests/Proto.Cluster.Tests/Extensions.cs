using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Proto.Cluster.Gossip;

namespace Proto.Cluster.Tests;

public static class Extensions
{
    public static Task<Pong> Ping(this Cluster cluster,
        string id,
        string message,
        CancellationToken token,
        string kind = EchoActor.Kind, ISenderContext senderContext = null) =>
        cluster.RequestAsync<Pong>(id, kind, new Ping { Message = message }, token);

    public static async Task<string> DumpClusterState(this IEnumerable<Cluster> members)
    {
        var sb = new StringBuilder();

        foreach (var c in members)
        {
            sb.AppendLine($"{Environment.NewLine}Member {c.System.Id}");

            if (c.System.Shutdown.IsCancellationRequested)
            {
                sb.AppendLine("\tStopped, reason: " + c.System.StoppedReason);

                continue;
            }

            var topology = await c.Gossip.GetState<ClusterTopology>(GossipKeys.Topology);

            sb.AppendLine("\tGossip topology:");

            foreach (var kvp in topology)
            {
                sb.AppendLine($"\t\tData {kvp.Key} - {kvp.Value.TopologyHash}");
            }

            sb.AppendLine("\tMember list:");

            foreach (var member in c.MemberList.GetMembers())
            {
                sb.AppendLine($"\t\t{member}");
            }

            sb.AppendLine("\tBlock list:");

            foreach (var member in c.Remote.BlockList.BlockedMembers)
            {
                sb.AppendLine($"\t\t{member}");
            }
        }

        return sb.ToString();
    }
}