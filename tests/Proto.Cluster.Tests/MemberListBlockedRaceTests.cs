using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.Cluster.Tests;

[Collection("ClusterTests")]
public class MemberListBlockedRaceTests
{
    [Fact]
    public async Task BlockingAMemberDoesNotDropMembersThatJoinedWhileItWasHandled()
    {
        var fixture = new SingleMemberClusterFixture();
        await using var _ = fixture;
        await fixture.InitializeAsync();

        var cluster = fixture.Members[0];
        var memberList = cluster.MemberList;
        var blockList = cluster.Remote.BlockList;
        var newcomer = new Member { Id = "newcomer", Host = "127.0.0.1", Port = 65000 };

        var memberListLock = typeof(MemberList).GetField("_lock", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(memberList)!;

        // Hold the member list lock while a member is blocked, so the MemberBlocked handler queues behind it, then apply a
        // topology update that adds a member, as a cluster provider would. The handler must not reapply the members it saw
        // before that update.
        Monitor.Enter(memberListLock);
        try
        {
            blockList.Block(new[] { "departed" }, "test");
            Thread.Sleep(500);
            memberList.UpdateClusterTopology(memberList.GetAllMembers().Append(newcomer).ToArray());
        }
        finally
        {
            Monitor.Exit(memberListLock);
        }

        await Task.Delay(1000);

        blockList.IsBlocked(newcomer.Id).Should().BeFalse();
        memberList.ContainsMemberId(newcomer.Id).Should().BeTrue();
        blockList.IsBlocked(cluster.System.Id).Should().BeFalse();
    }

    private class SingleMemberClusterFixture : BaseInMemoryClusterFixture
    {
        public SingleMemberClusterFixture() : base(1)
        {
        }
    }
}
