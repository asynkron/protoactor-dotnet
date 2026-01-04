// -----------------------------------------------------------------------
// <copyright file="FutureBatch.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.Metrics;

namespace Proto.Future;

/// <summary>
///     Intended for a single batch with a common CancellationToken.
/// </summary>
public sealed class FutureBatchProcess : Process, IDisposable
{
    private readonly CancellationTokenRegistration _cancellation;
    private readonly TaskCompletionSource<object>?[] _completionSources;
    private readonly KeyValuePair<string, object?>[] _metricTags = Array.Empty<KeyValuePair<string, object?>>();
    private readonly Action? _onTimeout;
    private int _prevIndex = -1;

    public FutureBatchProcess(ActorSystem system, int size, CancellationToken ct) : base(system)
    {
        var name = System.ProcessRegistry.NextId();
        var (pid, absent) = System.ProcessRegistry.TryAdd(name, this);

        if (!absent)
        {
            throw new ProcessNameExistException(name, pid);
        }

        Pid = pid;

        _completionSources = ArrayPool<TaskCompletionSource<object>>.Shared.Rent(size);

        if (system.Metrics.Enabled)
        {
            _metricTags = new KeyValuePair<string, object?>[] { new("id", System.Id), new("address", System.Address) };
            _onTimeout = () => ActorMetrics.FuturesTimedOutCount.Add(1, _metricTags);
        }
        else
        {
            _onTimeout = null;
        }

        if (ct != default)
        {
            _cancellation = ct.Register(() =>
                {
                    foreach (var completionSource in _completionSources)
                    {
                        if (completionSource?.TrySetException(
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

    public void Dispose()
    {
        _cancellation.Dispose();
        ArrayPool<TaskCompletionSource<object>?>.Shared.Return(_completionSources, true);
        System.ProcessRegistry.Remove(Pid);
    }

    public IFuture? TryGetFuture()
    {
        var index = Interlocked.Increment(ref _prevIndex);

        if (index < _completionSources.Length)
        {
            var completionSource = new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);
            _completionSources[index] = completionSource;

            if (System.Metrics.Enabled)
            {
                ActorMetrics.FuturesStartedCount.Add(1, _metricTags);
            }

            return new SimpleFutureHandle(Pid.WithRequestId(ToRequestId(index)), completionSource, _onTimeout);
        }

        return null;
    }

    protected internal override void SendUserMessage(PID pid, object message)
    {
        if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var completionSource))
        {
            return;
        }

        CompleteRequest(index, completionSource, message);
    }

    protected internal override void SendSystemMessage(PID pid, SystemMessage message)
    {
        if (message is Stop)
        {
            Dispose();

            return;
        }

        if (!TryGetTaskCompletionSource(pid.RequestId, out var index, out var completionSource))
        {
            return;
        }

        CompleteRequest(index, completionSource, default!);
    }

    private void CompleteRequest(int index, TaskCompletionSource<object> completionSource, object result)
    {
        try
        {
            completionSource.TrySetResult(result);
            _completionSources[index] = default;
        }
        finally
        {
            if (System.Metrics.Enabled)
            {
                ActorMetrics.FuturesCompletedCount.Add(1, _metricTags);
            }
        }
    }

    private bool TryGetIndex(uint requestId, out int index)
    {
        index = (int)(requestId - 1);

        return index >= 0 && index < _completionSources.Length;
    }

    private static uint ToRequestId(int index) => (uint)(index + 1);

    private bool TryGetTaskCompletionSource(uint requestId, out int index, out TaskCompletionSource<object> completionSource)
    {
        if (!TryGetIndex(requestId, out index))
        {
            completionSource = default!;

            return false;
        }

        completionSource = _completionSources[index]!;

        return completionSource != default!;
    }

    private sealed class SimpleFutureHandle : IFuture
    {
        private readonly Action? _onTimeout;

        public SimpleFutureHandle(PID pid, TaskCompletionSource<object> completionSource, Action? onTimeout)
        {
            _onTimeout = onTimeout;
            Pid = pid;
            CompletionSource = completionSource;
        }

        internal TaskCompletionSource<object> CompletionSource { get; }

        public PID Pid { get; }
        public Task<object> Task => CompletionSource.Task;

        public async Task<object> GetTask(CancellationToken cancellationToken)
        {
            try
            {
                if (cancellationToken == default)
                {
                    return await CompletionSource.Task.ConfigureAwait(false);
                }

                await using (cancellationToken.Register(() => CompletionSource.TrySetCanceled()).ConfigureAwait(false))
                {
                    return await CompletionSource.Task.ConfigureAwait(false);
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