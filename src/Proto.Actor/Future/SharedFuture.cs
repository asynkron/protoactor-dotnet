// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Buffers;
using System.Threading;
using System.Threading.Tasks;
using Proto.Metrics;

namespace Proto.Future
{
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

        public IFuture? TryCreateHandle()
        {
            if (Exhausted) return default;
        
            var index = Interlocked.Increment(ref _prevIndex);
        
            if (index >= _completionSources.Length) return default;
        
            var tcs = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completionSources[index] = tcs;
        
            if (index == _completionSources.Length - 1)
            {
                Exhausted = true;
            }
        
            return new SharedFutureHandle(this, ToRequestId(index), tcs);
        }

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

        private sealed class SharedFutureHandle : IFuture
        {
            private readonly SharedFutureProcess _process;
            private readonly uint _requestId;
            private readonly TaskCompletionSource<object> _tcs;

            public SharedFutureHandle(SharedFutureProcess process, uint requestId, TaskCompletionSource<object> tcs)
            {
                _process = process;
                _requestId = requestId;
                Pid = process.Pid.WithRequestId(requestId);
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
                    throw new TimeoutException("Request didn't receive any Response within the expected time.");
                }
                finally
                {
                    Interlocked.Increment(ref _process._completedRequests);
                    if (_process.Exhausted && _process.RequestsInFlight == 0)
                        _process._onCompleted(_process);
                }
            }

            public void Dispose() => _process.Cancel(_requestId);
        }
    }
}