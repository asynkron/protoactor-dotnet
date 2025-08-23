using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Proto.Cluster;
using Xunit;

namespace Proto.Cluster.Tests;

public class MemberListTests
{
    private static Member CreateMember(string id)
        => new() { Id = id, Host = "localhost", Port = 8080 };

    [Fact]
    public async Task ComputeTopologyChanges_FiltersBlockedAndDuplicates()
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

        var (active, left, joined) = MemberList.ComputeTopologyChanges(previous, current, blocked);

        Assert.Single(active.Members);
        Assert.Equal("4", active.Members.Single().Id);
        Assert.Equal(new[] { "1", "2" }, left.Members.Select(m => m.Id).OrderBy(x => x));
        Assert.Equal(new[] { "4" }, joined.Members.Select(m => m.Id));
    }
}
