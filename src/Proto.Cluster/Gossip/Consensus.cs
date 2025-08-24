// -----------------------------------------------------------------------
// <copyright file="ClusterConsensus.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster.Gossip;

public interface IConsensusHandle<T> : IDisposable where T : notnull
{
    Task<(bool consensus, T value)> TryGetConsensus(CancellationToken ct);

    Task<(bool consensus, T value)> TryGetConsensus(TimeSpan maxWait, CancellationToken cancellationToken);
}

internal class GossipConsensusHandle<T> : IConsensusHandle<T> where T : notnull
{
    private readonly Action _deregister;
    private TaskCompletionSource<T> _consensusTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public GossipConsensusHandle(Action deregister)
    {
        _deregister = deregister;
    }

    public async Task<(bool consensus, T value)> TryGetConsensus(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var t = Volatile.Read(ref _consensusTcs).Task;
            // ReSharper disable once MethodSupportsCancellation
            // Poll for consensus completion or timeout to periodically check cancellation
            await Task.WhenAny(t, Task.Delay(500, ct)).ConfigureAwait(false);

            if (t.IsCompleted)
            {
                return (true, t.Result);
            }
        }

        return (false, default!);
    }

    public async Task<(bool consensus, T value)> TryGetConsensus(TimeSpan maxWait, CancellationToken cancellationToken)
    {
        try
        {
            var result = await Volatile.Read(ref _consensusTcs).Task
                .WaitAsync(maxWait, cancellationToken)
                .ConfigureAwait(false);
            return (true, result);
        }
        catch (TimeoutException)
        {
            return (false, default!);
        }
        catch (OperationCanceledException)
        {
            return (false, default!);
        }
    }

    public void Dispose() => _deregister();

    internal void TrySetConsensus(object consensus)
    {
        var tcs = Volatile.Read(ref _consensusTcs);

        if (tcs.Task.IsCompleted && tcs.Task.Result?.Equals(consensus) != true)
        {
            TryResetConsensus();
        }

        //if not set, set it, if already set, keep it set
        tcs.TrySetResult((T)consensus);
    }

    public void TryResetConsensus()
    {
        //only replace if the task is completed
        var current = Volatile.Read(ref _consensusTcs);

        if (current.Task.IsCompleted)
        {
            var taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.CompareExchange(ref _consensusTcs, taskCompletionSource, current);
        }
    }
}