// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Proto.Metrics;

namespace Proto.Future
{
    public sealed class SharedFutureProcess : Process, IDisposable
    {
        private readonly FutureHandle[] _slots;
        private readonly ChannelWriter<FutureHandle> _completedFutures;
        private readonly ChannelReader<FutureHandle> _availableFutures;

        private long _createdRequests;
        private long _completedRequests;

        /// <summary>
        /// Highest request-id allowed before it wraps around.
        /// </summary>
        private readonly int _maxRequestId;

        private readonly ActorMetrics? _metrics;
        private readonly string[]? _metricLabels;
        private readonly Action? _onTimeout;
        private readonly Action? _onStarted;

        internal SharedFutureProcess(ActorSystem system, int size) : base(system)
        {
            var name = System.ProcessRegistry.NextId();
            var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

            if (!absent) throw new ProcessNameExistException(name, pid);

            Pid = pid;

            if (!system.Metrics.IsNoop)
            {
                _metrics = system.Metrics.Get<ActorMetrics>();
                _metricLabels = new[] {System.Id, System.Address};
                _onTimeout = () => _metrics.FuturesTimedOutCount.Inc(_metricLabels);
                _onStarted = () => _metrics.FuturesStartedCount.Inc(_metricLabels);
            }
            else
            {
                _onTimeout = null;
                _onStarted = null;
            }

            _slots = new FutureHandle[size];

            Channel<FutureHandle> channel = Channel.CreateUnbounded<FutureHandle>();
            _availableFutures = channel.Reader;
            _completedFutures = channel.Writer;

            for (var i = 0; i < _slots.Length; i++)
            {
                var requestSlot = new FutureHandle(this, ToRequestId(i));
                _slots[i] = requestSlot;
                _completedFutures.TryWrite(requestSlot);
            }

            _maxRequestId = (int.MaxValue - (int.MaxValue % size));
        }

        private PID Pid { get; }
        public bool Stopping { get; private set; }

        public int RequestsInFlight => (int) (_createdRequests - _completedRequests);

        public IFuture? TryCreateHandle()
        {
            if (Stopping || !_availableFutures.TryRead(out var requestSlot)) return default;

            var pid = requestSlot.Init();
            Interlocked.Increment(ref _createdRequests);
            _onStarted?.Invoke();
            return new SharedFutureHandle(this, pid, requestSlot.CompletionSource!);
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
            if (!TryGetRequestSlot(pid.RequestId, out var slot)) return;

            try
            {
                slot.CompletionSource!.TrySetResult(MessageEnvelope.UnwrapMessage(message)!);
            }
            finally
            {
                Complete(pid.RequestId, slot);
            }
        }

        protected internal override void SendSystemMessage(PID pid, object message)
        {
            if (message is Stop)
            {
                Dispose();
                return;
            }

            if (!TryGetRequestSlot(pid.RequestId, out var slot)) return;

            try
            {
                slot.CompletionSource!.TrySetResult(default!);
            }
            finally
            {
                Complete(pid.RequestId, slot);
            }
        }

        private void Complete(uint requestId, FutureHandle slot)
        {
            if (slot.TryComplete((int) requestId))
            {
                _completedFutures.TryWrite(slot);

                Interlocked.Increment(ref _completedRequests);
                _metrics?.FuturesCompletedCount.Inc(_metricLabels);

                if (Stopping && RequestsInFlight == 0)
                    Stop(Pid);
            }
        }

        public void Dispose()
        {
            System.ProcessRegistry.Remove(Pid);

            foreach (var requestSlot in _slots)
            {
                requestSlot.CompletionSource?.TrySetCanceled();
            }
        }

        public void Stop()
        {
            Stopping = true;
            _completedFutures.TryComplete();

            while (_availableFutures.TryRead(out _))
            {
            }

            if (RequestsInFlight == 0)
            {
                Stop(Pid);
            }
        }

        private void Cancel(uint requestId)
        {
            if (!TryGetRequestSlot(requestId, out var slot)) return;

            try
            {
                slot.CompletionSource?.TrySetCanceled();
            }
            finally
            {
                Complete(requestId, slot);
            }
        }

        private int GetIndex(uint requestId) => (int) (requestId - 1) % _slots.Length;

        private bool TryGetRequestSlot(uint requestId, out FutureHandle slot)
        {
            if (requestId == 0)
            {
                slot = default!;
                return false;
            }

            slot = _slots[GetIndex(requestId)];
            return slot.RequestId == requestId;
        }

        private static uint ToRequestId(int index) => (uint) (index + 1);

        private sealed class SharedFutureHandle : IFuture
        {
            private readonly SharedFutureProcess _parent;

            private readonly TaskCompletionSource<object> _tcs;

            public SharedFutureHandle(SharedFutureProcess parent, PID pid, TaskCompletionSource<object> tcs)
            {
                _parent = parent;
                Pid = pid;
                _tcs = tcs;
            }

            public PID Pid { get; }
            public Task<object> Task => _tcs.Task;

            public async Task<object> GetTask(CancellationToken cancellationToken)
            {
                try
                {
                    if (cancellationToken == default)
                    {
                        return await _tcs.Task;
                    }
                    
                    await using (cancellationToken.Register(() => _tcs.TrySetCanceled()))
                    {
                        return await _tcs.Task;
                    }
                }
                catch
                {
                    _parent.Cancel(Pid.RequestId);
                    _parent._onTimeout?.Invoke();
                    throw new TimeoutException("Request didn't receive any Response within the expected time.");
                }
            }

            public void Dispose() => _parent.Cancel(Pid.RequestId);
        }

        private class FutureHandle
        {
            public TaskCompletionSource<object>? CompletionSource { get; private set; }
            private int _requestId;
            public uint RequestId => (uint) _requestId;
            private readonly SharedFutureProcess _parent;

            public FutureHandle(SharedFutureProcess parent, uint requestId)
            {
                _parent = parent;
                _requestId = (int) requestId;
            }

            public bool TryComplete(int requestId)
            {
                var incBy = _parent._slots.Length;
                var nextRequestId = (int) ((requestId + incBy) % _parent._maxRequestId);

                if (requestId == Interlocked.CompareExchange(ref _requestId, nextRequestId, requestId))
                {
                    CompletionSource = null;
                    return true;
                }

                return false;
            }

            public PID Init()
            {
                CompletionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
                return _parent.Pid.WithRequestId(RequestId);
            }
        }
    }
}