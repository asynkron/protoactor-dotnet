using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;
using System.Threading;

namespace Proto.TestFixtures
{
    public class TestMailboxHandler : IMessageInvoker, IDispatcher
    {
        private AutoResetEvent _onScheduleCompleted;

        public List<Exception> EscalatedFailures { get; set; } = new List<Exception>();

        public Task InvokeSystemMessageAsync(object msg)
        {
            return ((TestMessage)msg).TaskCompletionSource.Task;
        }

        public Task InvokeUserMessageAsync(object msg)
        {
            return ((TestMessage)msg).TaskCompletionSource.Task;
        }

        public void EscalateFailure(Exception reason, object message)
        {
            EscalatedFailures.Add(reason);
        }

        public int Throughput { get; } = 10;

        public void Schedule(Func<Task> runner)
        {
            runner().Wait();
            _onScheduleCompleted?.Set();
        }

        /// <summary>
        /// Wraps around an action that resumes and waits for mailbox processing to finish
        /// </summary>
        /// <param name="resumeMailboxProcessing">A trigger that will cause message processing to resume</param>
        public void ResumeMailboxProcessingAndWait(Action resumeMailboxProcessing)
        {
            _onScheduleCompleted = new AutoResetEvent(false);
            resumeMailboxProcessing();
            _onScheduleCompleted.WaitOne();
        }
    }
}