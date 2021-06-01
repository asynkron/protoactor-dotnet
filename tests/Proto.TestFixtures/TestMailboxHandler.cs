using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestMailboxHandler : IMessageInvoker, IDispatcher
    {
        private readonly ConcurrentQueue<TaskCompletionSource<int>> _taskCompletionQueue =
            new();

        private TaskCompletionSource<bool> _hasFailures = new();
        public Task HasFailures => _hasFailures.Task;

        public List<Exception> EscalatedFailures { get; } = new();

        public int Throughput => 10;

        public async void Schedule(Func<Task> runner)
        {
            var waitingTaskExists = _taskCompletionQueue.TryDequeue(out var onScheduleCompleted);
            await runner();
            if (waitingTaskExists) onScheduleCompleted.SetResult(0);
        }

        public async ValueTask InvokeSystemMessageAsync(object msg) => await ((TestMessageWithTaskCompletionSource) msg).TaskCompletionSource.Task;

        public async ValueTask InvokeUserMessageAsync(object msg) => await ((TestMessageWithTaskCompletionSource) msg).TaskCompletionSource.Task;

        public void EscalateFailure(Exception reason, object message)
        {
            EscalatedFailures.Add(reason);
            _hasFailures.TrySetResult(true);
        }

        public CancellationTokenSource CancellationTokenSource { get; } = new();
        
    }
}