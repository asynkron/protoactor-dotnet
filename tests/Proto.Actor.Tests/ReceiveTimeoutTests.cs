using System;
using System.Threading;
using Xunit;

namespace Proto.Tests
{
    public class ReceiveTimeoutTests
    {
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
                            break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);
            
            Thread.Sleep(1500);
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
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);
            
            Thread.Sleep(1500);
            Assert.False(timeoutReceived);
        }
        
        [Fact]
        public void can_cancel_receive_timeout()
        {
            var timeoutReceived = false;
            var props = Actor.FromFunc((context) =>
            {
                switch (context.Message)
                {
                    case Started _:
                        context.SetReceiveTimeout(TimeSpan.FromMilliseconds(150));
                        context.CancelReceiveTimeout();
                        break;
                    case ReceiveTimeout _:
                        timeoutReceived = true;
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);
            
            Thread.Sleep(1500);
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
                        break;
                }
                return Actor.Done;
            });
            Actor.Spawn(props);
            
            Thread.Sleep(1500);
            Assert.True(timeoutReceived);
        }
    }
}
