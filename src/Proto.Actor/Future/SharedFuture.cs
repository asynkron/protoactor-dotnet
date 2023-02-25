// -----------------------------------------------------------------------
// <copyright file="Futures.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.Metrics;

namespace Proto.Future;

public sealed class SharedFutureProcess : Process, IDisposable
{
    private readonly ConcurrentBag<FutureHandle> _futures = new();

    /// <summary>
    ///     Highest request-id allowed before it wraps around.
    /// </summary>
    private readonly int _maxRequestId;

    private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();
    private readonly Action? _onStarted;
    private readonly Action? _onTimeout;
    private readonly FutureHandle[] _slots;
    private long _completedRequests;

    private long _createdRequests;

    internal SharedFutureProcess(ActorSystem system, int size) : base(system)
    {
        var name = System.ProcessRegistry.NextId();
        var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

        if (!absent)
        {
            throw new ProcessNameExistException(name, pid);
        }

        Pid = pid;

        if (system.Metrics.Enabled)
        {
            _metricTags = new KeyValuePair<string, object?>[] { new("id", System.Id), new("address", System.Address) };
            _onTimeout = () => ActorMetrics.FuturesTimedOutCount.Add(1, _metricTags);
            _onStarted = () => ActorMetrics.FuturesStartedCount.Add(1, _metricTags);
        }
        else
        {
            _onTimeout = null;
            _onStarted = null;
        }

        _slots = new FutureHandle[size];

        for (var i = 0; i < _slots.Length; i++)
        {
            var requestSlot = new FutureHandle(this, ToRequestId(i));
            _slots[i] = requestSlot;
            _futures.Add(requestSlot);
        }

        _maxRequestId = int.MaxValue - int.MaxValue % size;
    }

    private PID Pid { get; }
    public bool Stopping { get; private set; }

    public int RequestsInFlight
    {
        get
        {
            // Read completedRequests first and createdRequests later so that we will
            // never read the 2 vars in an order that would result in completedRequests > createdRequests.
            var completed = Interlocked.Read(ref _completedRequests);
            var created = Interlocked.Read(ref _createdRequests);

            return (int)(created - completed);
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

    public IFuture? TryCreateHandle()
    {
        if (Stopping || !_futures.TryTake(out var requestSlot))
        {
            return default;
        }

        var pid = requestSlot.Init();
        Interlocked.Increment(ref _createdRequests);
        _onStarted?.Invoke();

        return new SharedFutureHandle(this, pid, requestSlot.CompletionSource!);
    }

    protected internal override void SendUserMessage(PID pid, object message)
    {
        if (!TryGetRequestSlot(pid.RequestId, out var slot))
        {
            return;
        }

        try
        {
            slot.CompletionSource!.TrySetResult(message);
        }
        finally
        {
            Complete(pid.RequestId, slot);
        }
    }

    protected internal override void SendSystemMessage(PID pid, SystemMessage message)
    {
        if (message is Stop)
        {
            Dispose();

            return;
        }

        if (!TryGetRequestSlot(pid.RequestId, out var slot))
        {
            return;
        }

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
        if (slot.TryComplete((int)requestId))
        {
            _futures.Add(slot);

            Interlocked.Increment(ref _completedRequests);

            if (System.Metrics.Enabled)
            {
                ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);
            }

            if (Stopping && RequestsInFlight == 0)
            {
                Stop(Pid);
            }
        }
    }

    private void Cancel(uint requestId)
    {
        if (!TryGetRequestSlot(requestId, out var slot))
        {
            return;
        }

        try
        {
            slot.CompletionSource?.TrySetCanceled();
        }
        finally
        {
            Complete(requestId, slot);
        }
    }

    private int GetIndex(uint requestId) => (int)(requestId - 1) % _slots.Length;

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

    private static uint ToRequestId(int index) => (uint)(index + 1);

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
                    return await _tcs.Task.ConfigureAwait(false);
                }

                await using (cancellationToken.Register(() => _tcs.TrySetCanceled()).ConfigureAwait(false))
                {
                    return await _tcs.Task.ConfigureAwait(false);
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
        private readonly SharedFutureProcess _parent;
        private long _requestId;

        public FutureHandle(SharedFutureProcess parent, uint requestId)
        {
            _parent = parent;
            _requestId = (int)requestId;
        }

        public TaskCompletionSource<object>? CompletionSource { get; private set; }
        public uint RequestId => (uint)Interlocked.Read(ref _requestId);

        public bool TryComplete(int requestId)
        {
            var incBy = _parent._slots.Length;
            var nextRequestId = (requestId + incBy) % _parent._maxRequestId;

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