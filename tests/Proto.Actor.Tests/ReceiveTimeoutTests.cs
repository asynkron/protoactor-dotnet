using System;
using System.Threading;
using Xunit;

namespace Proto.Tests
{
    public class ReceiveTimeoutTests
    {
        private readonly AutoResetEvent _blockingWaiter = new AutoResetEvent(false);

        [Fact]
        public void receive_timeout_received_within_expected_time()
        {
            var timeoutReceived = false;
        
            var props = Actor.FromFunc((context) =>
            {
                switch (context.Message)
                {
                        case Started _:
                            context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                            break;
                        case ReceiveTimeout _:
                            timeoutReceived = true;
                            _blockingWaiter.Set();
                            break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);

            _blockingWaiter.WaitOne();
            Assert.True(timeoutReceived);
        }
        
        [Fact]
        public void receive_timeout_not_received_within_expected_time()
        {
            var timeoutReceived = false;
            var props = Actor.FromFunc((context) =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(1500));
                        _blockingWaiter.Set();
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);

            _blockingWaiter.WaitOne();
            Assert.False(timeoutReceived);
        }
        
        [Fact]
        public void can_cancel_receive_timeout()
        {
            var timeoutReceived = false;

            var endingTimeout = TimeSpan.MaxValue;

            var props = Actor.FromFunc((context) =>
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
                        _blockingWaiter.Set();
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);
            
            // this event should not be signaled
            var signaled = _blockingWaiter.WaitOne(1500);
            Assert.False(signaled);
            Assert.Equal(TimeSpan.Zero, endingTimeout);
            Assert.False(timeoutReceived);
        }
        
        [Fact]
        public void can_still_set_receive_timeout_after_cancelling()
        {
            var timeoutReceived = false;
            var props = Actor.FromFunc((context) =>
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
                        _blockingWaiter.Set();
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);

            _blockingWaiter.WaitOne();
            Assert.True(timeoutReceived);
        }
    }
}
