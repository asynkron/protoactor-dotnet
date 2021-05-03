// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
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
    public sealed class FutureFactory
    {
        private readonly ActorSystem System;

        public FutureFactory(ActorSystem system, CancellationToken cancellationToken = default)
        {
            System = system;
            Future = new ThreadLocal<SharedFutureProcess>(() => new SharedFutureProcess(System, 1000));
            cancellationToken.Register(() => {
                    foreach (var process in Future.Values)
                    {
                        process.Stop(process.Pid);
                    }
                }
            );
        }

        private ThreadLocal<SharedFutureProcess> Future { get; }

        // public IFuture GetHandle(CancellationToken ct) => SingleProcessHandle();

        private IFuture SingleProcessHandle() => new FutureProcess(System);

        // private IFuture SharedHandle(CancellationToken ct)
        // {
        //     var process = Future.Value!;
        //     var future = process.TryCreateHandle(ct);
        //
        //     if (future != default) return future;
        //
        //     Future.Value = process = new SharedFutureProcess(System, 1000);
        //     return process.TryCreateHandle(ct)!;
        // }
    }

    public interface IFuture : IDisposable
    {
        public PID Pid { get; }
        public Task<object> Task { get; }

        public Task<object> GetTask(CancellationToken cancellationToken);
    }

    sealed class SimpleFutureHandle : IFuture
    {
        private readonly Action _onTimeout;
        private readonly TaskCompletionSource<object> _tcs;

        public SimpleFutureHandle(PID pid, TaskCompletionSource<object> tcs, Action onTimeout)
        {
            _onTimeout = onTimeout;
            Pid = pid;
            _tcs = tcs;
        }

        public PID Pid { get; }
        public Task<object> Task => _tcs.Task;

        public async Task<object> GetTask(CancellationToken cancellationToken)
        {
            try
            {
                await using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
                {
                    return await _tcs.Task;
                }
            }
            catch
            {
                _onTimeout();
                throw new TimeoutException("Request didn't receive any Response within the expected time.");
            }
        }

        public void Dispose()
        {
        }
    }

    public sealed class SharedFutureProcess : Process, IDisposable
    {
        private readonly TaskCompletionSource<object>?[] _completionSources;
        private readonly ActorMetrics? _metrics;
        private long _prevCreatedRequests;
        private int _prevIndex = -1;
        private int _completedRequests;
        private readonly Action<SharedFutureProcess> _onCompleted;

        internal SharedFutureProcess(ActorSystem system, int size, Action<SharedFutureProcess>? onCompleted = null) : base(system)
        {
            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;

            _onCompleted = onCompleted ?? (process => Stop(process.Pid));

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metrics.FuturesStartedCount.Inc(new[] {system.Id, system.Address});
            }

            _completionSources = ArrayPool<TaskCompletionSource<object>>.Shared.Rent(size);
        }

        public PID Pid { get; }
        public bool Exhausted { get; private set; }

        public int RequestsInFlight => _prevIndex + 1 - _completedRequests;

        // public IFuture? TryCreateHandle(CancellationToken ct)
        // {
        //     if (Exhausted) return default;
        //
        //     var index = Interlocked.Increment(ref _prevIndex);
        //
        //     if (index >= _completionSources.Length) return default;
        //
        //     var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
        //     _completionSources[index] = tcs;
        //
        //     if (index == _completionSources.Length - 1)
        //     {
        //         Exhausted = true;
        //     }
        //
        //     return new SharedFutureHandle(this, ToRequestId(index), tcs, ct);
        // }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var tcs)) return;

            try
            {
                tcs.TrySetResult(MessageEnvelope.UnwrapMessage(message)!);
            }
            finally
            {
                _completionSources[index] = default;
                Interlocked.Increment(ref _completedRequests);
                _metrics?.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});

                if (Exhausted && RequestsInFlight == 0)
                    _onCompleted(this);
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
            }
            finally
            {
                _completionSources[index] = default;
                Interlocked.Increment(ref _completedRequests);
                _metrics?.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});

                if (Exhausted && RequestsInFlight == 0)
                    _onCompleted(this);
            }
        }

        public void Dispose()
        {
            System.ProcessRegistry.Remove(Pid);
            ArrayPool<TaskCompletionSource<object>?>.Shared.Return(_completionSources, true);
        }

        public void ReUse()
        {
            lock (_completionSources)
            {
                if (!Exhausted || RequestsInFlight > 0)
                {
                    throw new Exception("Invalid state to re-use process");
                }

                // Probably not needed?
                Array.Clear(_completionSources, 0, _completionSources.Length);
                Exhausted = false;
                _prevCreatedRequests += Math.Min(_completionSources.Length, (_prevIndex + 1));
                _prevIndex = -1;
                _completedRequests = 0;
            }
        }

        private void Cancel(uint requestId)
        {
            if (!TryGetTaskCompletionSource(requestId, out var index, out var tcs)) return;

            if (!tcs.Task.IsCompleted && tcs.TrySetCanceled())
            {
                _completionSources[index] = default;
                Interlocked.Increment(ref _completedRequests);
                if (Exhausted && RequestsInFlight == 0)
                    _onCompleted(this);
            }
        }

        private bool TryGetIndex(uint requestId, out int index)
        {
            index = (int) (requestId - 1 - _prevCreatedRequests);
            return index >= 0 && index < _completionSources.Length;
        }

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

        private uint ToRequestId(int index) => (uint) (index + 1 + _prevCreatedRequests);

        // private sealed class SharedFutureHandle : IFuture
        // {
        //     private readonly SharedFutureProcess _process;
        //     private readonly CancellationTokenRegistration _timeout;
        //     private readonly uint _requestId;
        //
        //     public SharedFutureHandle(SharedFutureProcess process, uint requestId, TaskCompletionSource<object> tcs, CancellationToken ct)
        //     {
        //         _process = process;
        //         _requestId = requestId;
        //         Pid = process.Pid.WithRequestId(requestId);
        //         Task = tcs.Task;
        //         _timeout = ct.Register(() => {
        //                 if (tcs.Task.IsCompleted) return;
        //
        //                 tcs.TrySetException(
        //                     new TimeoutException("Request didn't receive any Response within the expected time.")
        //                 );
        //
        //                 if (!_process.System.Metrics.IsNoop)
        //                 {
        //                     _process._metrics!.FuturesTimedOutCount.Inc(new[] {_process.System.Id, _process.System.Address});
        //                 }
        //
        //                 Interlocked.Increment(ref _process._completedRequests);
        //                 if (_process.Exhausted && _process.RequestsInFlight == 0)
        //                     _process._onCompleted(_process);
        //             }
        //         );
        //     }
        //
        //     public PID Pid { get; }
        //     public Task<object> Task { get; }
        //
        //     public void Dispose()
        //     {
        //         _process.Cancel(_requestId);
        //         _timeout.Dispose();
        //     }
        // }
    }

    /// <summary>
    /// Intended for a single batch with a common CancellationToken.
    /// </summary>
    public sealed class FutureBatchProcess : Process, IDisposable
    {
        private readonly TaskCompletionSource<object>?[] _completionSources;
        private readonly ActorMetrics? _metrics;
        private readonly CancellationTokenRegistration _cancellation;
        private readonly Action _onTimeout;
        private int _prevIndex = -1;

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
        }

        public PID Pid { get; }

        public bool TryGetFuture(out IFuture future)
        {
            var index = Interlocked.Increment(ref _prevIndex);

            if (index < _completionSources.Length)
            {
                var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                _completionSources[index] = tcs;
                _metrics?.FuturesStartedCount.Inc(new[] {System.Id, System.Address});
                future = new SimpleFutureHandle(Pid.WithRequestId(ToRequestId(index)), tcs, _onTimeout);
                return true;
            }

            future = default!;
            return false;
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