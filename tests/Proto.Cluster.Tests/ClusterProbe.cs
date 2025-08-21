using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using Google.Protobuf;

namespace Proto.Cluster.Tests;

/// <summary>
/// Utility helpers for probing cluster state in tests.
/// Uses the gossip consensus handles so tests can await
/// changes instead of relying on <see cref="Task.Delay"/>.
/// </summary>
public static class ClusterProbe
{
    /// <summary>
    /// Waits until all consensus handles report the same value.
    /// Throws <see cref="TimeoutException"/> if consensus is not
    /// reached before the timeout expires.
    /// </summary>
    public static async Task<T> WaitForConsensusAsync<T>(IEnumerable<IConsensusHandle<T>> handles,
        TimeSpan timeout, CancellationToken ct = default) where T : notnull
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var tasks = handles.Select(h => h.TryGetConsensus(timeout, cts.Token)).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        if (results.Any(r => !r.consensus))
        {
            throw new TimeoutException("Consensus was not reached within the allotted time.");
        }

        var value = results[0].value;
        if (results.Any(r => !EqualityComparer<T>.Default.Equals(r.value, value)))
        {
            throw new Exception("Consensus value differed between members.");
        }

        return value;
    }

    /// <summary>
    /// Waits until all consensus handles agree on the expected value.
    /// </summary>
    public static async Task WaitForConsensusAsync<T>(IEnumerable<IConsensusHandle<T>> handles,
        T expectedValue, TimeSpan timeout, CancellationToken ct = default) where T : notnull
    {
        var value = await WaitForConsensusAsync(handles, timeout, ct).ConfigureAwait(false);
        if (!EqualityComparer<T>.Default.Equals(value, expectedValue))
        {
            throw new TimeoutException("Consensus was reached for a different value than expected.");
        }
    }

    /// <summary>
    /// Waits until all consensus handles report lack of consensus.
    /// Useful when a cluster should fall out of consensus after a change.
    /// </summary>
    public static async Task WaitForNoConsensusAsync<T>(IEnumerable<IConsensusHandle<T>> handles,
        TimeSpan timeout, CancellationToken ct = default) where T : notnull
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        var tasks = handles.Select(async h =>
        {
            while (!cts.Token.IsCancellationRequested)
            {
                var (consensus, _) = await h.TryGetConsensus(TimeSpan.FromMilliseconds(200), cts.Token)
                    .ConfigureAwait(false);
                if (!consensus)
                {
                    return;
                }
            }

            cts.Token.ThrowIfCancellationRequested();
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
    }

    /// <summary>
    /// Waits for all members to reach topology consensus and returns the agreed topology hash.
    /// </summary>
    public static async Task<ulong> WaitForTopologyConsensusAsync(IEnumerable<Cluster> members,
        TimeSpan timeout, CancellationToken ct = default)
    {
        var handles = members
            .Select(m => m.Gossip.RegisterConsensusCheck<ClusterTopology, ulong>(GossipKeys.Topology,
                t => t.TopologyHash))
            .ToList();
        try
        {
            return await WaitForConsensusAsync(handles, timeout, ct).ConfigureAwait(false);
        }
        finally
        {
            foreach (var h in handles)
            {
                h.Dispose();
            }
        }
    }

    /// <summary>
    /// Polls a member's gossip state until the value from the specified source member satisfies the predicate.
    /// </summary>
    public static async Task WaitForMemberStateAsync<TState>(Cluster observer, string key, string sourceMemberId,
        Func<TState, bool> predicate, TimeSpan timeout, CancellationToken ct = default) where TState : class, IMessage, new()
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);

        while (!cts.Token.IsCancellationRequested)
        {
            var state = await observer.Gossip.GetState<TState>(key).ConfigureAwait(false);
            if (state.TryGetValue(sourceMemberId, out var value) && predicate(value))
            {
                return;
            }

            try
            {
                await Task.Delay(200, cts.Token).ConfigureAwait(false);
            }
            catch (TaskCanceledException)
            {
                // ignore and allow loop to exit on next iteration
            }
        }

        throw new TimeoutException("Expected state not observed within the allotted time.");
    }
}

