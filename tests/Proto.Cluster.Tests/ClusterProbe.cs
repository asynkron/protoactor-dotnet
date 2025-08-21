using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Proto.Cluster;
using Proto.Cluster.Gossip;
using ClusterTest.Messages;

namespace Proto.Cluster.Tests;

/// <summary>
/// Utility helpers for probing cluster state in tests.
/// Uses gossip consensus handles so tests can await changes
/// instead of relying on <see cref="Task.Delay"/>.
/// </summary>
public sealed class ClusterProbe
{
    private readonly Cluster _cluster;

    public ClusterProbe(Cluster cluster) => _cluster = cluster;

    /// <summary>
    /// Writes a random gossip state for the provided key on this cluster instance.
    /// Returns the generated value so callers can verify consensus later.
    /// </summary>
    public async Task<string> PublishRandomStateAsync(string key)
    {
        var value = Guid.NewGuid().ToString("N");
        await _cluster.Gossip.SetStateAsync(key, new SomeGossipState { Key = value }).ConfigureAwait(false);
        return value;
    }

    /// <summary>
    /// Waits for the cluster to reach consensus on the expected value for the specified state key.
    /// </summary>
    public async Task WaitForStateConsensusAsync(string key, string expectedValue, TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var handle = _cluster.Gossip.RegisterConsensusCheck<SomeGossipState, string>(key, s => s.Key);
        await WaitForConsensusAsync(handle, expectedValue, timeout, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Creates a state probe that publishes a random value and can wait for consensus on it.
    /// </summary>
    public async Task<StateProbe> CreateStateProbeAsync(string? key = null)
    {
        key ??= Guid.NewGuid().ToString("N");
        var handle = _cluster.Gossip.RegisterConsensusCheck<SomeGossipState, string>(key, s => s.Key);
        var value = await PublishRandomStateAsync(key).ConfigureAwait(false);
        return new StateProbe(_cluster, handle, key, value);
    }

    /// <summary>
    /// Waits until all consensus handles report the same value.
    /// Throws <see cref="TimeoutException"/> if consensus is not reached before the timeout expires.
    /// </summary>
    public static async Task<T> WaitForConsensusAsync<T>(IEnumerable<IConsensusHandle<T>> handles, TimeSpan timeout,
        CancellationToken ct = default) where T : notnull
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        var tasks = handles.Select(h => h.TryGetConsensus(cts.Token)).ToList();
        var results = await Task.WhenAll(tasks).ConfigureAwait(false);

        if (cts.IsCancellationRequested && results.Any(r => !r.consensus))
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
    /// Waits until the consensus handle reports the expected value.
    /// </summary>
    public static async Task WaitForConsensusAsync<T>(IConsensusHandle<T> handle, T expectedValue, TimeSpan timeout,
        CancellationToken ct = default) where T : notnull
    {
        var value = await WaitForConsensusAsync(new[] { handle }, timeout, ct).ConfigureAwait(false);
        if (!EqualityComparer<T>.Default.Equals(value, expectedValue))
        {
            throw new TimeoutException("Consensus was reached for a different value than expected.");
        }
    }

    /// <summary>
    /// Waits for all consensus handles to agree on the expected value.
    /// </summary>
    public static async Task WaitForConsensusAsync<T>(IEnumerable<IConsensusHandle<T>> handles, T expectedValue,
        TimeSpan timeout, CancellationToken ct = default) where T : notnull
    {
        var value = await WaitForConsensusAsync(handles, timeout, ct).ConfigureAwait(false);
        if (!EqualityComparer<T>.Default.Equals(value, expectedValue))
        {
            throw new TimeoutException("Consensus was reached for a different value than expected.");
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

    /// <summary>
    /// Represents a published test state that can await consensus.
    /// </summary>
    public sealed class StateProbe : IDisposable
    {
        private readonly Cluster _cluster;
        private readonly IConsensusHandle<string> _handle;

        internal StateProbe(Cluster cluster, IConsensusHandle<string> handle, string key, string value)
        {
            _cluster = cluster;
            _handle = handle;
            Key = key;
            Value = value;
        }

        public string Key { get; }
        public string Value { get; private set; }

        public Task WaitForConsensus(TimeSpan timeout, CancellationToken ct = default) =>
            ClusterProbe.WaitForConsensusAsync(_handle, Value, timeout, ct);

        public async Task<string> PublishRandomValueAsync()
        {
            var newValue = Guid.NewGuid().ToString("N");
            await _cluster.Gossip.SetStateAsync(Key, new SomeGossipState { Key = newValue }).ConfigureAwait(false);
            Value = newValue;
            return newValue;
        }

        public void Dispose() => _handle.Dispose();
    }
}

