using System.Collections.Generic;
using Proto.Mailbox;
using Xunit;

namespace Proto.Tests
{
    public class EventStreamTests
    {
        [Fact]
        public void EventStream_CanSubscribeToSpecificEventTypes()
        {
            var received = "";
            var eventStream = new EventStream();
            eventStream.Subscribe<string>(theString => received = theString);
            eventStream.Publish("hello");
            Assert.Equal("hello", received);
        }

        [Fact]
        public void EventStream_CanSubscribeToAllEventTypes()
        { 
            var receivedEvents = new List<object>();
            var eventStream = new EventStream();
            eventStream.Subscribe(@event => receivedEvents.Add(@event));
            eventStream.Publish("hello");
            eventStream.Publish(1);
            eventStream.Publish(true);
            Assert.Equal(3, receivedEvents.Count);
        }

        [Fact]
        public void EventStream_CanUnsubscribeFromEvents()
        {
            var receivedEvents = new List<object>();
            var eventStream = new EventStream();
            var subscription = eventStream.Subscribe<string>(@event => receivedEvents.Add(@event));
            eventStream.Publish("first message");
            subscription.Unsubscribe();
            eventStream.Publish("second message");
            Assert.Equal(1, receivedEvents.Count);
        }

        [Fact]
        public void EventStream_OnlyReceiveSubscribedToEventTypes()
        {
            var eventsReceived = new List<object>();
            var eventStream = new EventStream();
            eventStream.Subscribe<int>(@event => eventsReceived.Add(@event));
            eventStream.Publish("not an int");
            Assert.Equal(0, eventsReceived.Count);
        }

        [Fact]
        public void EventStream_CanSubscribeToSpecificEventTypes_Async()
        {
            string received;
            var eventStream = new EventStream();
            eventStream.Subscribe<string>(theString =>
            {
                received = theString;
                Assert.Equal("hello", received);
            }, Dispatchers.DefaultDispatcher);
            eventStream.Publish("hello");
        }
    }
}
