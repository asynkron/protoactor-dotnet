using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster;
using Xunit;

namespace Proto.Cluster.Tests;

public class ClusterTopologyBuilderTests
{
    private static Member CreateMember(string id)
        => new() { Id = id, Host = "localhost", Port = 8000 + int.Parse(id) };

    [Fact]
    public async Task Compute_FiltersBlockedAndDuplicates()
    {
        var m1 = CreateMember("1");
        var m2 = CreateMember("2");
        var previous = new ImmutableMemberSet(new[] { m1, m2 });

        var blocked = ImmutableHashSet.Create(m2.Id);

        var dupOld = CreateMember("3");
        await Task.Delay(1);
        var dupYoung = CreateMember("4");
        dupYoung.Host = dupOld.Host;
        dupYoung.Port = dupOld.Port;

        var current = new List<Member> { m2, dupOld, dupYoung };

        var changes = ClusterTopologyBuilder.Compute(previous, current, blocked);

        Assert.Single(changes.ActiveMembers.Members);
        Assert.Equal("4", changes.ActiveMembers.Members.Single().Id);
        Assert.Equal(new[] { "1", "2" }, changes.Left.Members.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(new[] { "4" }, changes.Joined.Members.Select(m => m.Id));
    }

    [Fact]
    public void BuildTopology_ReportsJoinedAndLeftMembers()
    {
        var m1 = CreateMember("1");
        var m2 = CreateMember("2");
        var m3 = CreateMember("3");
        var previous = new ImmutableMemberSet(new[] { m1, m2 });
        var current = new List<Member> { m2, m3 };
        var blocked = ImmutableHashSet<string>.Empty;

        var changes = ClusterTopologyBuilder.Compute(previous, current, blocked);
        var topology = ClusterTopologyBuilder.BuildTopology(changes, blocked, CancellationToken.None);

        Assert.Equal(new[] { "1" }, topology.Left.Select(m => m.Id));
        Assert.Equal(new[] { "3" }, topology.Joined.Select(m => m.Id));
        Assert.Equal(new[] { "2", "3" }, topology.Members.Select(m => m.Id).OrderBy(x => x));
    }

    [Fact]
    public void BuildTopology_WhenNoChanges_LeavesJoinedSetsEmpty()
    {
        var m1 = CreateMember("1");
        var m2 = CreateMember("2");
        var previous = new ImmutableMemberSet(new[] { m1, m2 });
        var current = new List<Member> { m1, m2 };
        var blocked = ImmutableHashSet<string>.Empty;

        var changes = ClusterTopologyBuilder.Compute(previous, current, blocked);
        var topology = ClusterTopologyBuilder.BuildTopology(changes, blocked, CancellationToken.None);

        Assert.Empty(topology.Left);
        Assert.Empty(topology.Joined);
        Assert.Equal(new[] { "1", "2" }, topology.Members.Select(m => m.Id).OrderBy(x => x));
    }
}
