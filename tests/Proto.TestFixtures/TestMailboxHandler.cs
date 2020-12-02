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

        public List<Exception> EscalatedFailures { get; set; } = new();

        public int Throughput { get; } = 10;

        public void Schedule(Func<Task> runner)
        {
            var waitingTaskExists = _taskCompletionQueue.TryDequeue(out var onScheduleCompleted);
            runner().ContinueWith(t =>
                {
                    if (waitingTaskExists) onScheduleCompleted.SetResult(0);
                }
            );
        }

        public Task InvokeSystemMessageAsync(object msg) => ((TestMessage) msg).TaskCompletionSource.Task;

        public Task InvokeUserMessageAsync(object msg) => ((TestMessage) msg).TaskCompletionSource.Task;

        public void EscalateFailure(Exception reason, object message)
        {
            EscalatedFailures.Add(reason);
        }

        public CancellationTokenSource CancellationTokenSource { get; } = new();

        /// <summary>
        ///     Wraps around an action that resumes and waits for mailbox processing to finish
        /// </summary>
        /// <param name="resumeMailboxProcessing">A trigger that will cause message processing to resume</param>
        /// <param name="timeoutMs">The waiting task will be cancelled after the timeout expires</param>
        public Task ResumeMailboxProcessingAndWaitAsync(Action resumeMailboxProcessing, int timeoutMs = 60000)
        {
            var onScheduleCompleted = new TaskCompletionSource<int>();
            _taskCompletionQueue.Enqueue(onScheduleCompleted);

            resumeMailboxProcessing();

            var ct = new CancellationTokenSource();
            ct.Token.Register(() => onScheduleCompleted.TrySetCanceled());
            ct.CancelAfter(timeoutMs);

            return onScheduleCompleted.Task
                // suppress any TaskCanceledException to let the test continue
                .ContinueWith(t => t);
        }
    }
}