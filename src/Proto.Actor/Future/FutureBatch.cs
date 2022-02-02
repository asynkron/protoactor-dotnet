// -----------------------------------------------------------------------
// <copyright file="FutureBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Buffers;
using System.Collections.Generic;
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
        private readonly CancellationTokenRegistration _cancellation;
        private readonly Action? _onTimeout;
        private int _prevIndex = -1;
        private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();

        public FutureBatchProcess(ActorSystem system, int size, CancellationToken ct) : base(system)
        {
            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;

            _completionSources = ArrayPool<TaskCompletionSource<object>>.Shared.Rent(size);

            if (system.Metrics.Enabled)
            {
                _metricTags = new KeyValuePair<string, object?>[] {new("id", System.Id), new("address", System.Address)};
                _onTimeout = () => ActorMetrics.FuturesTimedOutCount.Add(1, _metricTags);
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

                if (System.Metrics.Enabled)
                    ActorMetrics.FuturesStartedCount.Add(1, _metricTags);

                return new SimpleFutureHandle(Pid.WithRequestId(ToRequestId(index)), tcs, _onTimeout);
            }

            return null;
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var tcs)) return;

            try
            {
                tcs.TrySetResult(message);
                _completionSources[index] = default;
            }
            finally
            {
                if(System.Metrics.Enabled)
                    ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);
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
                if(System.Metrics.Enabled)
                   ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);
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