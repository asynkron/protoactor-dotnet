using System;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class ReceiveTimeoutTests
    {
        private static readonly RootContext Context = new RootContext();
        [Fact]
        public async Task receive_timeout_received_within_expected_time()
        {
            var timeoutReceived = false;
            var receiveTimeoutWaiter = GetExpiringTaskCompletionSource();

            var props = Props.FromFunc((context) =>
            {
                switch (context.Message)
                {
                        case Started _:
                            context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                            break;
                        case ReceiveTimeout _:
                            timeoutReceived = true;
                            receiveTimeoutWaiter.SetResult(0);
                            break;
                }
                return Actor.Done;
            });
            Context.Spawn(props);

            await GetSafeAwaitableTask(receiveTimeoutWaiter);
            Assert.True(timeoutReceived);
        }
        
        [Fact]
        public async Task receive_timeout_not_received_within_expected_time()
        {
            var timeoutReceived = false;
            var actorStartedWaiter = GetExpiringTaskCompletionSource();

            var props = Props.FromFunc((context) =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(1500));
                        actorStartedWaiter.SetResult(0);
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        break;
                }
                return Actor.Done;
            });
            Context.Spawn(props);

            await GetSafeAwaitableTask(actorStartedWaiter);
            Assert.False(timeoutReceived);
        }
        
        [Fact]
        public async Task can_cancel_receive_timeout()
        {
            var timeoutReceived = false;
            var endingTimeout = TimeSpan.MaxValue;
            var autoExpiringWaiter = GetExpiringTaskCompletionSource(1500);

            var props = Props.FromFunc((context) =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        context.CancelReceiveTimeout();
                        endingTimeout = context.ReceiveTimeout;
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        autoExpiringWaiter.SetResult(0); // should never happen
                        break;
                }
                return Actor.Done;
            });
            Context.Spawn(props);

            // this task should auto cancel
            await GetSafeAwaitableTask(autoExpiringWaiter);

            Assert.True(autoExpiringWaiter.Task.IsCanceled);
            Assert.Equal(TimeSpan.Zero, endingTimeout);
            Assert.False(timeoutReceived);
        }
        
        [Fact]
        public async Task can_still_set_receive_timeout_after_cancelling()
        {
            var timeoutReceived = false;
            var receiveTimeoutWaiter = GetExpiringTaskCompletionSource();

            var props = Props.FromFunc((context) =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        context.CancelReceiveTimeout();
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        receiveTimeoutWaiter.SetResult(0);
                        break;
                }
                return Actor.Done;
            });
            Context.Spawn(props);

            await GetSafeAwaitableTask(receiveTimeoutWaiter);
            Assert.True(timeoutReceived);
        }

        private TaskCompletionSource<int> GetExpiringTaskCompletionSource(int timeoutMs = 60000)
        {
            var tcs = new TaskCompletionSource<int>();
            var ct = new CancellationTokenSource();
            ct.Token.Register(() => tcs.TrySetCanceled());
            ct.CancelAfter(timeoutMs);
            return tcs;
        }

        private ConfiguredTaskAwaitable<Task<int>> GetSafeAwaitableTask(TaskCompletionSource<int> tcs)
        {
            return tcs.Task
                .ContinueWith(t => t) // suppress any TaskCanceledException
                .ConfigureAwait(false);
        }
    }
}
