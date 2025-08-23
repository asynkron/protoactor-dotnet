using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.Extensions.Logging;
using Proto.Logging;

namespace Proto.Cluster;

internal record TopologyChanges(
    ImmutableMemberSet ActiveMembers,
    ImmutableMemberSet Left,
    ImmutableMemberSet Joined);

internal static class ClusterTopologyBuilder
{
    private static readonly ILogger Logger = Log.CreateLogger<MemberList>();

    internal static TopologyChanges Compute(
        ImmutableMemberSet previousMembers,
        IReadOnlyCollection<Member> newMembers,
        ImmutableHashSet<string> blockedMembers)
    {
        var active = new ImmutableMemberSet(newMembers.ToArray()).Except(blockedMembers);
        active = RemoveDuplicateAddresses(active);
        var left = previousMembers.Except(active);
        var joined = active.Except(previousMembers);
        return new TopologyChanges(active, left, joined);
    }

    internal static ClusterTopology BuildTopology(
        TopologyChanges changes,
        ImmutableHashSet<string> blockedMembers,
        CancellationToken token)
        => new()
        {
            TopologyHash = changes.ActiveMembers.TopologyHash,
            Members = { changes.ActiveMembers.Members },
            Left = { changes.Left.Members },
            Joined = { changes.Joined.Members },
            Blocked = { blockedMembers },
            TopologyValidityToken = token
        };

    private static ImmutableMemberSet RemoveDuplicateAddresses(ImmutableMemberSet activeMembers)
    {
        var duplicateAddresses = activeMembers.Members.ToLookup(m => m.Address);
        foreach (var dup in duplicateAddresses.Where(d => d.Count() > 1))
        {
            var youngest = dup.OrderByDescending(m => m.Age).First();
            var rest = dup.Where(m => m.Id != youngest.Id).Select(m => m.Id).ToArray();

            Logger.DuplicateAddressFound(dup.Key, rest);
            activeMembers = activeMembers.Except(rest);
        }

        return activeMembers;
    }
}

