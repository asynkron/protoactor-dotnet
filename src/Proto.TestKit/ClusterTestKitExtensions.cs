using System;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster;

namespace Proto.TestKit;

/// <summary>
/// Extension methods for cluster-related test helpers.
/// </summary>
public static class ClusterTestKitExtensions
{
    /// <summary>
    /// Waits until the cluster's topology consensus hash changes.
    /// </summary>
    /// <param name="cluster">Cluster to monitor.</param>
    /// <param name="timeout">Maximum time to wait for the update. Defaults to 20 seconds.</param>
    /// <returns>The new topology hash once it differs from the initial hash.</returns>
    public static async Task<ulong> ExpectUpdatedTopologyConsensus(this Proto.Cluster.Cluster cluster, TimeSpan? timeout = null)
    {
        var waitTimeout = timeout ?? TimeSpan.FromSeconds(20);
        // Allow each consensus check up to five seconds to complete
        var ct = CancellationTokens.FromSeconds(5);

        // Capture the initial topology hash
        (_, var initialTopologyHash) = await cluster.MemberList.TopologyConsensus(ct).ConfigureAwait(false);
        ulong updatedTopologyHash = initialTopologyHash;

        await TestKit.AwaitConditionAsync(async () =>
        {
            (_, updatedTopologyHash) = await cluster.MemberList.TopologyConsensus(ct).ConfigureAwait(false);
            return updatedTopologyHash != initialTopologyHash;
        }, waitTimeout, $"Topology hash did not change within {waitTimeout}");

        return updatedTopologyHash;
    }

    /// <summary>
    /// Waits until the cluster's member list contains the specified <paramref name="member"/>.
    /// </summary>
    /// <param name="cluster">Cluster to inspect.</param>
    /// <param name="member">Member expected to exist in the cluster.</param>
    /// <param name="timeout">Maximum time to wait. Defaults to 10 seconds.</param>
    /// <returns>A task that completes when the member is present.</returns>
    public static Task ExpectMemberToExist(this Proto.Cluster.Cluster cluster, Member member, TimeSpan? timeout = null)
    {
        var waitTimeout = timeout ?? TimeSpan.FromSeconds(10);
        return TestKit.AwaitConditionAsync(
            () => cluster.MemberList.ContainsMemberId(member.Id),
            waitTimeout,
            $"Member {member.Id} was not found within {waitTimeout}");
    }
}
