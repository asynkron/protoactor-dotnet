// -----------------------------------------------------------------------
// <copyright file="FutureBatchProcess.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
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
        private readonly ActorMetrics? _metrics;
        private readonly CancellationTokenRegistration _cancellation;
        private readonly Action _onTimeout;

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
                _onTimeout = () => _metrics.FuturesTimedOutCount.Inc(new[] {System.Id, System.Address});
            }
            else
            {
                _onTimeout = () => { };
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
                                _onTimeout();
                            }
                        }
                    }
                );
            }

            Futures = GetFutures();
        }

        public PID Pid { get; }

        public IEnumerable<IFuture> Futures { get; }

        private IEnumerable<IFuture> GetFutures()
        {
            for (var i = 0; i < _completionSources.Length; i++)
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completionSources[i] = tcs;
                _metrics?.FuturesStartedCount.Inc(new[] {System.Id, System.Address});
                yield return new SimpleFutureHandle(Pid.WithRequestId(ToRequestId(i)), tcs, _onTimeout);
            }
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
                _metrics?.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
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
                _metrics?.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
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

        private uint ToRequestId(int index) => (uint) (index + 1);

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
    }
}