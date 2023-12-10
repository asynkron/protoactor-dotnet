using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;
using Xunit;

namespace Proto.Tests;

public class EventStreamTests
{
    [Fact]
    public async Task EventStream_CanSubscribeToSpecificEventTypes()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var eventStream = system.EventStream;
        var received = "";

        eventStream.Subscribe<string>(theString => received = theString);
        eventStream.Publish("hello");
        
        await Task.Delay(1000);
        
        Assert.Equal("hello", received);
    }

    [Fact]
    public async Task EventStream_CanSubscribeToAllEventTypes()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var eventStream = system.EventStream;
        var receivedEvents = new List<object>();

        eventStream.Subscribe(@event => receivedEvents.Add(@event));
        eventStream.Publish("hello");
        eventStream.Publish(1);
        eventStream.Publish(true);

        await Task.Delay(1000);
        
        Assert.Equal(3, receivedEvents.Count);
    }

    [Fact]
    public async Task EventStream_CanUnsubscribeFromEvents()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var eventStream = system.EventStream;
        var receivedEvents = new List<object>();
        var subscription = eventStream.Subscribe<string>(@event => receivedEvents.Add(@event));
        eventStream.Publish("first message");
        subscription.Unsubscribe();
        eventStream.Publish("second message");
        
        await Task.Delay(1000);
        
        Assert.Single(receivedEvents);
    }

    [Fact]
    public async Task EventStream_OnlyReceiveSubscribedToEventTypes()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var eventStream = system.EventStream;

        var eventsReceived = new List<object>();
        eventStream.Subscribe<int>(@event => eventsReceived.Add(@event));
        eventStream.Publish("not an int");
        
        await Task.Delay(1000);
        
        Assert.Empty(eventsReceived);
    }

    [Fact]
    public async Task EventStream_CanSubscribeToSpecificEventTypes_Async()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var eventStream = system.EventStream;

        string received;

        eventStream.Subscribe<string>(theString =>
            {
                received = theString;
                Assert.Equal("hello", received);
            }, Dispatchers.DefaultDispatcher
        );

        eventStream.Publish("hello");
        await Task.Delay(1000);
    }
}