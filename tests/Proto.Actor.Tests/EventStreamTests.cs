using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;
using Xunit;

namespace Proto.Tests
{
    public class EventStreamTests
    {
        [Fact]
        public async Task EventStream_CanSubscribeToSpecificEventTypes()
        {
            var received = "";
            var eventStream = new EventStream();
            eventStream.Subscribe<string>(theString => received = theString);
            await eventStream.PublishAsync("hello");
            Assert.Equal("hello", received);
        }

        [Fact]
        public async Task EventStream_CanSubscribeToAllEventTypes()
        { 
            var receivedEvents = new List<object>();
            var eventStream = new EventStream();
            eventStream.Subscribe(@event => receivedEvents.Add(@event));
            await eventStream.PublishAsync("hello");
            await eventStream.PublishAsync(1);
            await eventStream.PublishAsync(true);
            Assert.Equal(3, receivedEvents.Count);
        }

        [Fact]
        public async Task EventStream_CanUnsubscribeFromEvents()
        {
            var receivedEvents = new List<object>();
            var eventStream = new EventStream();
            var subscription = eventStream.Subscribe<string>(@event => receivedEvents.Add(@event));
            await eventStream.PublishAsync("first message");
            subscription.Unsubscribe();
            await eventStream.PublishAsync("second message");
            Assert.Equal(1, receivedEvents.Count);
        }

        [Fact]
        public async Task EventStream_OnlyReceiveSubscribedToEventTypes()
        {
            var eventsReceived = new List<object>();
            var eventStream = new EventStream();
            eventStream.Subscribe<int>(@event => eventsReceived.Add(@event));
            await eventStream.PublishAsync("not an int");
            Assert.Equal(0, eventsReceived.Count);
        }

        [Fact]
        public async Task EventStream_CanSubscribeToSpecificEventTypes_Async()
        {
            var received = "";
            var eventStream = new EventStream();
            eventStream.Subscribe<string>(theString =>
            {
                received = theString;
                Assert.Equal("hello", received);
            }, Dispatchers.DefaultDispatcher);
            await eventStream.PublishAsync("hello");
        }
    }
}
