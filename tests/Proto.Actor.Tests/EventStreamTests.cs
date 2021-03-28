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
            var system = new ActorSystem();
            var eventStream = system.EventStream;
            var received = "";

            eventStream.Subscribe<string>(theString => received = theString);
            eventStream.Publish("hello");
            Assert.Equal("hello", received);
        }

        [Fact]
        public void EventStream_CanSubscribeToAllEventTypes()
        {
            var system = new ActorSystem();
            var eventStream = system.EventStream;
            var receivedEvents = new List<object>();
            
            eventStream.Subscribe(@event => receivedEvents.Add(@event));
            eventStream.Publish("hello");
            eventStream.Publish(1);
            eventStream.Publish(true);
            Assert.Equal(3, receivedEvents.Count);
        }

        [Fact]
        public void EventStream_CanUnsubscribeFromEvents()
        {
            var system = new ActorSystem();
            var eventStream = system.EventStream;
            var receivedEvents = new List<object>();
            var subscription = eventStream.Subscribe<string>(@event => receivedEvents.Add(@event));
            eventStream.Publish("first message");
            subscription.Unsubscribe();
            eventStream.Publish("second message");
            Assert.Single(receivedEvents);
        }

        [Fact]
        public void EventStream_OnlyReceiveSubscribedToEventTypes()
        {
            var system = new ActorSystem();
            var eventStream = system.EventStream;
            
            var eventsReceived = new List<object>();
            eventStream.Subscribe<int>(@event => eventsReceived.Add(@event));
            eventStream.Publish("not an int");
            Assert.Empty(eventsReceived);
        }

        [Fact]
        public void EventStream_CanSubscribeToSpecificEventTypes_Async()
        {
            var system = new ActorSystem();
            var eventStream = system.EventStream;
            
            string received;
            eventStream.Subscribe<string>(theString => {
                    received = theString;
                    Assert.Equal("hello", received);
                }, Dispatchers.DefaultDispatcher
            );
            eventStream.Publish("hello");
        }
    }
}