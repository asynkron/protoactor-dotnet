using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestMailboxHandler : IMessageInvoker, IDispatcher
    {
        public List<Exception> EscalatedFailures { get; set; } = new List<Exception>();

        public Task InvokeSystemMessageAsync(object msg)
        {
            return ((TestMessage)msg).TaskCompletionSource.Task;
        }

        public Task InvokeUserMessageAsync(object msg)
        {
            return ((TestMessage)msg).TaskCompletionSource.Task;
        }

        public Task EscalateFailureAsync(Exception reason, object message)
        {
            EscalatedFailures.Add(reason);
            return Actor.Done;
        }

        public int Throughput { get; } = 10;

        public void Schedule(Func<Task> runner)
        {
            runner();
        }
    }
}