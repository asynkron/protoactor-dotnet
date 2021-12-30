// -----------------------------------------------------------------------
// <copyright file="ClusterConsensus.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Utils;

namespace Proto.Cluster.Gossip
{
    public interface IConsensusHandle<T> : IDisposable
    {
        Task<(bool consensus, T value)> TryGetConsensus(CancellationToken ct);

        Task<(bool consensus, T value)> TryGetConsensus(TimeSpan maxWait, CancellationToken cancellationToken);
    }

    internal class GossipConsensusHandle<T> : IConsensusHandle<T>
    {
        private TaskCompletionSource<T> _consensus = new(TaskCreationOptions.RunContinuationsAsynchronously);

        private readonly Action _deregister;

        public GossipConsensusHandle(Action deregister) => _deregister = deregister;

        internal void TrySetConsensus(object consensus)
        {
            if (_consensus.Task.IsCompleted && _consensus.Task.Result?.Equals(consensus) != true)
            {
                TryResetConsensus();
            }

            //if not set, set it, if already set, keep it set
            _consensus.TrySetResult((T) consensus);
        }

        public void TryResetConsensus()
        {
            //only replace if the task is completed
            var current = _consensus;

            if (current.Task.IsCompleted)
            {
                var taskCompletionSource = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
                Interlocked.CompareExchange(ref _consensus, taskCompletionSource, current);
            }
        }

        public async Task<(bool consensus, T value)> TryGetConsensus(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                var t = _consensus.Task;
                // ReSharper disable once MethodSupportsCancellation
                await Task.WhenAny(t, Task.Delay(500));
                if (t.IsCompleted)
                    return (true, t.Result);
            }

            return (false, default);
        }

        public Task<(bool consensus, T value)> TryGetConsensus(TimeSpan maxWait, CancellationToken cancellationToken)
            => _consensus.Task.WaitUpTo(maxWait, cancellationToken);

        public void Dispose() => _deregister();
    }
}