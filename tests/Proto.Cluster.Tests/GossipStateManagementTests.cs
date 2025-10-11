using FluentAssertions;
using Google.Protobuf.WellKnownTypes;
using Proto.Cluster.Gossip;
using Xunit;

namespace Proto.Cluster.Tests;

public class GossipStateManagementTests
{
    [Fact]
    public void MergeStates_should_merge_without_mutating_inputs()
    {
        // local state with one value
        var localState = new GossipState();
        var localMember = new GossipState.Types.GossipMemberState();
        localMember.Values.Add("k1", new GossipKeyValue
        {
            SequenceNumber = 1,
            Value = Any.Pack(new StringValue { Value = "old" }),
            LocalTimestampUnixMilliseconds = 100
        });
        localState.Members.Add("A", localMember);

        // remote state with new values and a new member
        var remoteState = new GossipState();
        var remoteMemberA = new GossipState.Types.GossipMemberState();
        remoteMemberA.Values.Add("k1", new GossipKeyValue
        {
            SequenceNumber = 2,
            Value = Any.Pack(new StringValue { Value = "new" }),
            LocalTimestampUnixMilliseconds = 200
        });
        remoteMemberA.Values.Add("k2", new GossipKeyValue
        {
            SequenceNumber = 1,
            Value = Any.Pack(new StringValue { Value = "other" }),
            LocalTimestampUnixMilliseconds = 300
        });
        remoteState.Members.Add("A", remoteMemberA);

        var remoteMemberB = new GossipState.Types.GossipMemberState();
        remoteMemberB.Values.Add("k3", new GossipKeyValue
        {
            SequenceNumber = 1,
            Value = Any.Pack(new StringValue { Value = "b1" }),
            LocalTimestampUnixMilliseconds = 400
        });
        remoteState.Members.Add("B", remoteMemberB);

        var (newState, updates, updatedKeys) = GossipStateManagement.MergeStates(localState, remoteState);

        // new state contains merged data
        newState.Members.Should().ContainKey("A").WhoseValue.Values.Should().ContainKeys("k1", "k2");
        newState.Members.Should().ContainKey("B");
        newState.Members["A"].Values["k1"].SequenceNumber.Should().Be(2);
        newState.Members["A"].Values["k1"].Value.Unpack<StringValue>().Value.Should().Be("new");
        newState.Members["A"].Values["k2"].SequenceNumber.Should().Be(1);
        newState.Members["A"].Values["k2"].Value.Unpack<StringValue>().Value.Should().Be("other");
        newState.Members["B"].Values["k3"].SequenceNumber.Should().Be(1);
        newState.Members["B"].Values["k3"].Value.Unpack<StringValue>().Value.Should().Be("b1");

        // updates reflect added/changed keys
        updates.Should().HaveCount(3);
        updates.Should().Contain(u => u.MemberId == "A" && u.Key == "k1" && u.SequenceNumber == 2);
        updates.Should().Contain(u => u.MemberId == "A" && u.Key == "k2" && u.SequenceNumber == 1);
        updates.Should().Contain(u => u.MemberId == "B" && u.Key == "k3" && u.SequenceNumber == 1);
        updatedKeys.Should().BeEquivalentTo(new[] { "k1", "k2", "k3" });

        // input states remain untouched
        localState.Members["A"].Values.Should().ContainKey("k1");
        localState.Members["A"].Values.Should().NotContainKey("k2");
        localState.Members["A"].Values["k1"].SequenceNumber.Should().Be(1);
        localState.Members["A"].Values["k1"].LocalTimestampUnixMilliseconds.Should().Be(100);
        remoteState.Members["A"].Values["k1"].LocalTimestampUnixMilliseconds.Should().Be(200);
        remoteState.Members["A"].Values["k2"].LocalTimestampUnixMilliseconds.Should().Be(300);
        remoteState.Members["B"].Values["k3"].LocalTimestampUnixMilliseconds.Should().Be(400);
    }
}
