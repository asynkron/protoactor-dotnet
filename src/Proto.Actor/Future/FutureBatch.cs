// -----------------------------------------------------------------------
// <copyright file="FutureBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Proto.Metrics;

namespace Proto.Future
{
    /// <summary>
    /// Intended for a single batch with a common CancellationToken.
    /// </summary>
    public sealed class FutureBatchProcess : Process, IDisposable
    {
        private readonly TaskCompletionSource<object>?[] _completionSources;
        private readonly ActorMetrics? _metrics;
        private readonly CancellationTokenRegistration _cancellation;
        private readonly Action? _onTimeout;
        private int _prevIndex = -1;
        private readonly string[]? _metricLabels;

        public FutureBatchProcess(ActorSystem system, int size, CancellationToken ct) : base(system)
        {
            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;

            _completionSources = ArrayPool<TaskCompletionSource<object>>.Shared.Rent(size);

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metricLabels = new[] {System.Id, System.Address};
                _onTimeout = () => _metrics.FuturesTimedOutCount.Inc(_metricLabels);
            }
            else
            {
                _onTimeout = null;
            }

            if (ct != default)
            {
                _cancellation = ct.Register(() => {
                        foreach (var tcs in _completionSources)
                        {
                            if (tcs?.TrySetException(
                                new TimeoutException("Request didn't receive any Response within the expected time.")
                            ) == true)
                            {
                                _onTimeout?.Invoke();
                            }
                        }
                    }
                );
            }
        }

        public PID Pid { get; }

        public IFuture? TryGetFuture()
        {
            var index = Interlocked.Increment(ref _prevIndex);

            if (index < _completionSources.Length)
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completionSources[index] = tcs;
                _metrics?.FuturesStartedCount.Inc(_metricLabels);
                return new SimpleFutureHandle(Pid.WithRequestId(ToRequestId(index)), tcs, _onTimeout);
            }

            return null;
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var tcs)) return;

            try
            {
                tcs.TrySetResult(MessageEnvelope.UnwrapMessage(message)!);
                _completionSources[index] = default;
            }
            finally
            {
                _metrics?.FuturesCompletedCount.Inc(_metricLabels);
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                Dispose();
                return;
            }

            if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var tcs)) return;

            try
            {
                tcs.TrySetResult(default!);
                _completionSources[index] = default;
            }
            finally
            {
                _metrics?.FuturesCompletedCount.Inc(_metricLabels);
            }
        }

        public void Dispose()
        {
            _cancellation.Dispose();
            ArrayPool<TaskCompletionSource<object>?>.Shared.Return(_completionSources, true);
            System.ProcessRegistry.Remove(Pid);
        }

        private bool TryGetIndex(uint requestId, out int index)
        {
            index = (int) (requestId - 1);
            return index >= 0 && index < _completionSources.Length;
        }

        private static uint ToRequestId(int index) => (uint) (index + 1);

        private bool TryGetTaskCompletionSource(uint requestId, out int index, out TaskCompletionSource<object> tcs)
        {
            if (!TryGetIndex(requestId, out index))
            {
                tcs = default!;
                return false;
            }

            tcs = _completionSources[index]!;
            return tcs != default!;
        }

        sealed class SimpleFutureHandle : IFuture
        {
            private readonly Action? _onTimeout;
            internal TaskCompletionSource<object> Tcs { get; }

            public SimpleFutureHandle(PID pid, TaskCompletionSource<object> tcs, Action? onTimeout)
            {
                _onTimeout = onTimeout;
                Pid = pid;
                Tcs = tcs;
            }

            public PID Pid { get; }
            public Task<object> Task => Tcs.Task;

            public async Task<object> GetTask(CancellationToken cancellationToken)
            {
                try
                {
                    if (cancellationToken == default)
                    {
                        return await Tcs.Task;
                    }
                    
                    await using (cancellationToken.Register(() => Tcs.TrySetCanceled()))
                    {
                        return await Tcs.Task;
                    }
                }
                catch
                {
                    _onTimeout?.Invoke();
                    throw new TimeoutException("Request didn't receive any Response within the expected time.");
                }
            }

            public void Dispose()
            {
            }
        }
    }
}