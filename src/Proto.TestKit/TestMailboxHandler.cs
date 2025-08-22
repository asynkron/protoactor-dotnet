using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// Simple mailbox handler used in tests to capture failures and control scheduling.
/// </summary>
public class TestMailboxHandler : IMessageInvoker, IDispatcher
{
    private readonly TaskCompletionSource<bool> _hasFailures = new();

    private readonly ConcurrentQueue<TaskCompletionSource<int>> _taskCompletionQueue =
        new();

    /// <summary>
    /// Task that completes when <see cref="EscalateFailure"/> is invoked.
    /// </summary>
    public Task HasFailures => _hasFailures.Task;

    /// <summary>
    /// Collected failures that were escalated during processing.
    /// </summary>
    public List<Exception> EscalatedFailures { get; } = new();

    public int Throughput => 10;

    public async void Schedule(Func<Task> runner)
    {
        var waitingTaskExists = _taskCompletionQueue.TryDequeue(out var onScheduleCompleted);
        await runner();

        if (waitingTaskExists)
        {
            onScheduleCompleted!.SetResult(0);
        }
    }

    // ReSharper disable once SuspiciousTypeConversion.Global
    public async Task InvokeSystemMessageAsync(SystemMessage msg) =>
        await ((TestMessageWithTaskCompletionSource)msg).TaskCompletionSource.Task;

    public async Task InvokeUserMessageAsync(object msg) =>
        await ((TestMessageWithTaskCompletionSource)msg).TaskCompletionSource.Task;

    public void EscalateFailure(Exception reason, object? message)
    {
        EscalatedFailures.Add(reason);
        _hasFailures.TrySetResult(true);
    }

    public CancellationTokenSource CancellationTokenSource { get; } = new();
}
