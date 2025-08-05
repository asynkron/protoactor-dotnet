using System.Threading.Tasks;
using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto;
using Proto.Cluster.Gossip;
using Proto.Utils;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class GracefulLeaveTests
{
    [Fact]
    public async Task GracefullyLeavingMemberIsBlockedAndGossipRejected()
    {
        var fixture = new TwoMembersClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var leaver = fixture.Members[0];
        var other = fixture.Members[1];

        await leaver.Gossip.SetStateAsync(GossipKeys.GracefullyLeft, new Empty());

        var interval = leaver.Config.GossipInterval;
        await Task.Delay(interval + interval);

        other.Remote.BlockList.BlockedMembers.Should().Contain(leaver.System.Id);

        var pid = PID.FromAddress(other.System.Address, Gossiper.GossipActorName);
        var response = await other.System.Root.RequestAsync<GossipResponse>(
            pid,
            new GossipRequest
            {
                MemberId = leaver.System.Id,
                State = new GossipState()
            },
            CancellationTokens.FromSeconds(5));

        response.Rejected.Should().BeTrue();
    }

    private class TwoMembersClusterFixture : BaseInMemoryClusterFixture
    {
        public TwoMembersClusterFixture() : base(2)
        {
        }
    }
}

