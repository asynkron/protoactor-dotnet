using System.Threading.Tasks;
using FluentAssertions;
using Proto.Cluster.Partition;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class StoppingMemberActivationTests
{
    [Fact]
    public async Task StoppingMemberRefusesNewActivations()
    {
        var fixture = new SingleMemberClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var member = fixture.Members[0];
        var placementActor = PartitionManager.RemotePartitionPlacementActor(member.System.Address);

        member.MemberList.Stopping = true;
        var refused = await member.System.Root.RequestAsync<ActivationResponse>(placementActor,
            new ActivationRequest { ClusterIdentity = ClusterIdentity.Create("refused", EchoActor.Kind) },
            CancellationTokens.FromSeconds(5));

        refused.Failed.Should().BeTrue();
        refused.Pid.Should().BeNull();

        member.MemberList.Stopping = false;
        var spawned = await member.System.Root.RequestAsync<ActivationResponse>(placementActor,
            new ActivationRequest { ClusterIdentity = ClusterIdentity.Create("spawned", EchoActor.Kind) },
            CancellationTokens.FromSeconds(5));

        spawned.Failed.Should().BeFalse();
        spawned.Pid.Should().NotBeNull();
    }

    private class SingleMemberClusterFixture : BaseInMemoryClusterFixture
    {
        public SingleMemberClusterFixture() : base(1)
        {
        }
    }
}
