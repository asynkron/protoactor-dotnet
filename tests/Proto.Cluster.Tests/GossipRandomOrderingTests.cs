using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using FluentAssertions;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

public class GossipRandomOrderingTests
{
    private static readonly int[] values = new[] { 1, 0, 2 };

    [Fact]
    public async Task SendState_UsesRandomProviderOrdering()
    {
        var rnd = new DeterministicRandomProvider(values);
        const string myId = "me";
        var gossip = new Gossip.Gossip(myId, gossipFanout: 3, gossipMaxSend: 10, logger: null,
            () => ImmutableHashSet.Create(myId, "a", "b", "c"), false, rnd);

        var topology = new ClusterTopology
        {
            Members =
            {
                new Member { Id = myId },
                new Member { Id = "a" },
                new Member { Id = "b" },
                new Member { Id = "c" }
            }
        };

        await gossip.UpdateClusterTopology(topology);
        gossip.SetState("k", new SomeGossipState { Key = "v" });

        var sends = gossip.SendState().ToList();
        sends.Select(s => s.member.Id).Should().Equal("b", "a", "c");

        foreach (var send in sends)
        {
            send.memberState.State.Members.Should().ContainKey(myId);
            send.memberState.State.Members[myId].Values.Should().ContainKey("k");
        }
    }
}
