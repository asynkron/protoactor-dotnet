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
    public sealed class FutureFactory
    {
        private readonly ActorSystem _system;

        public FutureFactory(ActorSystem system, CancellationToken cancellationToken = default)
        {
            _system = system;
            Future = new ThreadLocal<SharedFutureProcess>(() => new SharedFutureProcess(_system, 100));
            cancellationToken.Register(() => {
                    foreach (var process in Future.Values)
                    {
                        process.Stop(process.Pid);
                    }
                }
            );
        }

        private ThreadLocal<SharedFutureProcess> Future { get; }

        public IFuture GetHandle()
        {
            // return new FutureHandle(new FutureProcess(_system));
            var process = Future.Value!;
            var future = process.TryCreateHandle();
            
            if (future != default) return future;
            
            Future.Value = process = new SharedFutureProcess(_system, 1000);
            return process.TryCreateHandle()!;
        }
    }

    public interface IFuture : IDisposable
    {
        public PID Pid { get; }
        public Task<object> Task { get; }
    }

    public sealed class FutureHandle : IFuture
    {
        public FutureHandle(FutureProcess process)
        {
            Pid = process.Pid;
            Task = process.Task;
        }

        public PID Pid { get; }
        public Task<object> Task { get; }

        public void Dispose()
        {
        }
    };

    public sealed class SharedFutureHandle : IFuture
    {
        private readonly SharedFutureProcess _process;
        private readonly uint _requestId;

        public SharedFutureHandle(SharedFutureProcess process, uint requestId, Task<object> task)
        {
            _process = process;
            _requestId = requestId;
            Pid = process.Pid.WithRequestId(requestId);
            Task = task;
        }

        public PID Pid { get; }
        public Task<object> Task { get; }

        public void Dispose() => _process.Cancel(_requestId);
    };

    public sealed class SharedFutureProcess : Process, IDisposable
    {
        private readonly TaskCompletionSource<object>?[] _completionSources;
        private readonly ActorMetrics? _metrics;
        private readonly ActorSystem _system;
        private long _prevCreatedRequests;
        private int _prevIndex = -1;
        private int _completedRequests;
        private readonly Action<SharedFutureProcess> _onCompleted;

        internal SharedFutureProcess(ActorSystem system, int size, Action<SharedFutureProcess>? onCompleted = null) : base(system)
        {
            _system = system;

            _onCompleted = onCompleted ?? (process => Stop(process.Pid));

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metrics.FuturesStartedCount.Inc(new[] {system.Id, system.Address});
            }

            _completionSources = ArrayPool<TaskCompletionSource<object>>.Shared.Rent(size);

            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;
        }

        public PID Pid { get; }
        public bool Exhausted { get; private set; }

        public int RequestsInFlight => _prevIndex + 1 - _completedRequests;

        public SharedFutureHandle? TryCreateHandle()
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

            return new SharedFutureHandle(this, (uint) (index + 1 + _prevCreatedRequests), tcs.Task);

            // if (cancellationToken != default)
            // {
            //     cancellationToken.Register(() => {
            //             if (tcs.Task.IsCompleted) return;
            //
            //             tcs.TrySetException(new TimeoutException("Request didn't receive any Response within the expected time."));
            //
            //             Remove(requestId);
            //
            //             if (!_system.Metrics.IsNoop)
            //             {
            //                 _metrics!.FuturesTimedOutCount.Inc(new[] {System.Id, _system.Address});
            //             }
            //
            //             Interlocked.Increment(ref _completedRequests);
            //             if (_stopping && (_createdRequests - _completedRequests) == 0)
            //                 Stop(Pid);
            //         }
            //         , false
            //     );
            // }
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            if (pid.RequestId == default) // Not a response
            {
                return;
            }

            var index = Index(pid.RequestId);

            if (index < 0 || index >= _completionSources.Length)
            {
                //Out of bounds, could be late arriving. Log?
                return;
            }

            var tcs = _completionSources[index];
            if (tcs == default) return;

            try
            {
                tcs.TrySetResult(MessageEnvelope.UnwrapMessage(message)!);
                _completionSources[index] = default;
                Interlocked.Increment(ref _completedRequests);
            }
            finally
            {
                if (!_system.Metrics.IsNoop)
                {
                    _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
                }

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

            foreach (var tcs in _completionSources)
            {
                if (tcs != default && !tcs.Task.IsCompleted)
                {
                    tcs.TrySetResult(default!);
                }
            }
            // if (_ct == default || !_ct.IsCancellationRequested) _tcs.TrySetResult(default!);

            if (!_system.Metrics.IsNoop)
            {
                _metrics!.FuturesCompletedCount.Inc(new[] {System.Id, System.Address});
            }

            Stop(pid);
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

        public void Cancel(uint requestId)
        {
            var index = Index(requestId);

            if (index < 0 || index >= _completionSources.Length)
            {
                //Out of bounds
                return;
            }

            var tcs = _completionSources[index];
            if (tcs == default) return;

            if (!tcs.Task.IsCompleted && tcs.TrySetCanceled())
            {
                Interlocked.Increment(ref _completedRequests);
                if (Exhausted && RequestsInFlight == 0)
                    _onCompleted(this);
            }
        }

        private int Index(uint requestId) => (int) (requestId - 1 - _prevCreatedRequests);
    }
}