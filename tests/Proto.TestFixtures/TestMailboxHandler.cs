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

        public List<Exception> EscalatedFailures { get; } = new();

        public int Throughput => 10;

        public async void Schedule(Func<Task> runner)
        {
            var waitingTaskExists = _taskCompletionQueue.TryDequeue(out var onScheduleCompleted);
            await runner();
            if (waitingTaskExists) onScheduleCompleted.SetResult(0);
        }

        public async ValueTask InvokeSystemMessageAsync(object msg)
        {
            await Task.Yield();
            await ((TestMessageWithTaskCompletionSource) msg).TaskCompletionSource.Task;
        }

        public async ValueTask InvokeUserMessageAsync(object msg)
        {
            await Task.Yield();
            await ((TestMessageWithTaskCompletionSource) msg).TaskCompletionSource.Task;
        }

        public void EscalateFailure(Exception reason, object message) => EscalatedFailures.Add(reason);

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        /// <summary>
        ///     Wraps around an action that resumes and waits for mailbox processing to finish
        /// </summary>
        /// <param name="resumeMailboxProcessing">A trigger that will cause message processing to resume</param>
        /// <param name="timeoutMs">The waiting task will be cancelled after the timeout expires</param>
        public async Task ResumeMailboxProcessingAndWaitAsync(Action resumeMailboxProcessing, int timeoutMs = 60000)
        {
            var onScheduleCompleted = new TaskCompletionSource<int>();
            _taskCompletionQueue.Enqueue(onScheduleCompleted);

            resumeMailboxProcessing();

            var ct = new CancellationTokenSource();
            ct.Token.Register(() => onScheduleCompleted.TrySetCanceled());
            ct.CancelAfter(timeoutMs);

            await onScheduleCompleted.Task;
        }
    }
}