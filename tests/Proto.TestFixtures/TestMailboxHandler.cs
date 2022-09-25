using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures;

public class TestMailboxHandler : IMessageInvoker, IDispatcher
{
    private readonly TaskCompletionSource<bool> _hasFailures = new();

    private readonly ConcurrentQueue<TaskCompletionSource<int>> _taskCompletionQueue =
        new();

    public Task HasFailures => _hasFailures.Task;

    public List<Exception> EscalatedFailures { get; } = new();

    public int Throughput => 10;

    public async void Schedule(Func<Task> runner)
    {
        var waitingTaskExists = _taskCompletionQueue.TryDequeue(out var onScheduleCompleted);
        await runner();

        if (waitingTaskExists)
        {
            onScheduleCompleted.SetResult(0);
        }
    }

    // ReSharper disable once SuspiciousTypeConversion.Global
    public async ValueTask InvokeSystemMessageAsync(SystemMessage msg) =>
        await ((TestMessageWithTaskCompletionSource)msg).TaskCompletionSource.Task;

    public async ValueTask InvokeUserMessageAsync(object msg) =>
        await ((TestMessageWithTaskCompletionSource)msg).TaskCompletionSource.Task;

    public void EscalateFailure(Exception reason, object message)
    {
        EscalatedFailures.Add(reason);
        _hasFailures.TrySetResult(true);
    }

    public CancellationTokenSource CancellationTokenSource { get; } = new();
}