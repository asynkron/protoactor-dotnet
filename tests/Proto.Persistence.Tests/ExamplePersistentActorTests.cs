using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Persistence.Tests
{
    public class ExamplePersistentActorTests
    {
        private const int InitialState = 1;

        [Fact]
        public async Task EventsAreSavedToPersistence()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, actorId, providerState) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 2});
            await providerState
                .GetEventsAsync(actorId, 0, long.MaxValue, o => {
                        Assert.IsType<Multiplied>(o);
                        Assert.Equal(2, ((Multiplied) o).Amount);
                    }
                );
        }

        [Fact]
        public async Task SnapshotsAreSavedToPersistence()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, actorId, providerState) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 10});
            context.Send(pid, new RequestSnapshot());
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            var snapshotState = snapshot as State;
            Assert.Equal(10, snapshotState.Value);
        }

        [Fact]
        public async Task EventsCanBeDeleted()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, actorId, providerState) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 10});
            await providerState.DeleteEventsAsync(actorId, 1);
            var events = new List<object>();
            await providerState.GetEventsAsync(actorId, 0, long.MaxValue, v => events.Add(v));

            Assert.Empty(events);
        }

        [Fact]
        public async Task SnapshotsCanBeDeleted()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, actorId, providerState) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 10});
            context.Send(pid, new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 1);
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            Assert.Null(snapshot);
        }

        [Fact]
        public async Task GivenEventsOnly_StateIsRestoredFromEvents()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, _, _) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 2});
            var state = await RestartActorAndGetState(pid, props, context);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async Task GivenASnapshotOnly_StateIsRestoredFromTheSnapshot()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, actorId, providerState) = CreateTestActor(context);
            await providerState.PersistSnapshotAsync(actorId, 0, new State {Value = 10});
            var state = await RestartActorAndGetState(pid, props, context);
            Assert.Equal(10, state);
        }

        [Fact]
        public async Task GivenEventsThenASnapshot_StateShouldBeRestoredFromTheSnapshot()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, _, _) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            var state = await RestartActorAndGetState(pid, props, context);
            var expectedState = InitialState * 2 * 2;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async Task GivenASnapshotAndSubsequentEvents_StateShouldBeRestoredFromSnapshotAndSubsequentEvents()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, _, _) = CreateTestActor(context);
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            context.Send(pid, new Multiply {Amount = 4});
            context.Send(pid, new Multiply {Amount = 8});
            var state = await RestartActorAndGetState(pid, props, context);
            var expectedState = InitialState * 2 * 2 * 4 * 8;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async Task GivenMultipleSnapshots_StateIsRestoredFromMostRecentSnapshot()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, actorId, providerState) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            context.Send(pid, new Multiply {Amount = 4});
            context.Send(pid, new RequestSnapshot());
            await providerState.DeleteEventsAsync(actorId, 2); // just to be sure state isn't recovered from events
            var state = await RestartActorAndGetState(pid, props, context);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async Task GivenMultipleSnapshots_DeleteSnapshotObeysIndex()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, actorId, providerState) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            context.Send(pid, new Multiply {Amount = 4});
            context.Send(pid, new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 0);
            await providerState.DeleteEventsAsync(actorId, 1);
            var state = await RestartActorAndGetState(pid, props, context);
            var expectedState = InitialState * 2 * 4;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async Task GivenASnapshotAndEvents_WhenSnapshotDeleted_StateShouldBeRestoredFromEvents()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, actorId, providerState) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            context.Send(pid, new Multiply {Amount = 4});
            context.Send(pid, new Multiply {Amount = 8});
            await providerState.DeleteSnapshotsAsync(actorId, 3);

            var state = await RestartActorAndGetState(pid, props, context);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async Task Index_IncrementsOnEventsSaved()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, _, _) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(0, index);
            context.Send(pid, new Multiply {Amount = 4});
            index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
        }

        [Fact]
        public async Task Index_IsIncrementedByTakingASnapshot()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, _, _) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new RequestSnapshot());
            context.Send(pid, new Multiply {Amount = 4});
            var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(2, index);
        }

        [Fact]
        public async Task Index_IsCorrectAfterRecovery()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, props, _, _) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 4});

            await context.StopAsync(pid);
            pid = context.Spawn(props);
            var state = await context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromSeconds(1));
            var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async Task GivenEvents_CanReplayFromStartIndexToEndIndex()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var (pid, _, actorId, providerState) = CreateTestActor(context);

            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 2});
            context.Send(pid, new Multiply {Amount = 4});
            context.Send(pid, new Multiply {Amount = 8});
            var messages = new List<object>();
            await providerState.GetEventsAsync(actorId, 1, 2, msg => messages.Add(msg));
            Assert.Equal(2, messages.Count);
            Assert.Equal(2, ((Multiplied) messages[0]).Amount);
            Assert.Equal(4, ((Multiplied) messages[1]).Amount);
        }

        [Fact]
        public async Task CanUseSeparateStores()
        {
            await using var system = new ActorSystem();
            var context = system.Root;
            
            var actorId = Guid.NewGuid().ToString();
            var eventStore = new InMemoryProvider();
            var snapshotStore = new InMemoryProvider();
            var props = Props.FromProducer(() => new ExamplePersistentActor(eventStore, snapshotStore, actorId))
                .WithMailbox(() => new TestMailbox());
            var pid = context.Spawn(props);

            context.Send(pid, new Multiply {Amount = 2});
            var eventStoreMessages = new List<object>();
            var snapshotStoreMessages = new List<object>();
            await eventStore.GetEventsAsync(actorId, 0, 1, msg => eventStoreMessages.Add(msg));
            Assert.Single(eventStoreMessages);
            await snapshotStore.GetEventsAsync(actorId, 0, 1, msg => snapshotStoreMessages.Add(msg));
            Assert.Empty(snapshotStoreMessages);
        }

        private (PID pid, Props props, string actorId, IProvider provider) CreateTestActor(RootContext context)
        {
            var actorId = Guid.NewGuid().ToString();
            var inMemoryProvider = new InMemoryProvider();
            var props = Props
                .FromProducer(() => new ExamplePersistentActor(inMemoryProvider, inMemoryProvider, actorId))
                .WithMailbox(() => new TestMailbox());
            var pid = context.Spawn(props);
            return (pid, props, actorId, inMemoryProvider);
        }

        private async Task<int> RestartActorAndGetState(PID pid, Props props, RootContext context)
        {
            await context.StopAsync(pid);
            pid = context.Spawn(props);
            return await context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromMilliseconds(500));
        }
    }

    class State
    {
        public int Value { get; set; }
    }

    class GetState
    {
    }

    class GetIndex
    {
    }

    class Multiply
    {
        public int Amount { get; set; }
    }

    class Multiplied
    {
        public int Amount { get; set; }
    }

    class RequestSnapshot
    {
    }

    class ExamplePersistentActor : IActor
    {
        private readonly Persistence _persistence;
        private State _state = new() {Value = 1};

        public ExamplePersistentActor(IEventStore eventStore, ISnapshotStore snapshotStore, string persistenceId) => _persistence =
            Persistence.WithEventSourcingAndSnapshotting(eventStore, snapshotStore, persistenceId,
                ApplyEvent, ApplySnapshot
            );

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    await _persistence.RecoverStateAsync();
                    break;
                case GetState _:
                    context.Respond(_state.Value);
                    break;
                case GetIndex _:
                    context.Respond(_persistence.Index);
                    break;
                case RequestSnapshot _:
                    await _persistence.PersistSnapshotAsync(new State {Value = _state.Value});
                    break;
                case Multiply msg:
                    await _persistence.PersistEventAsync(new Multiplied {Amount = msg.Amount});
                    break;
            }
        }

        private void ApplyEvent(Event @event)
        {
            switch (@event.Data)
            {
                case Multiplied msg:
                    _state.Value = _state.Value * msg.Amount;
                    break;
            }
        }

        private void ApplySnapshot(Snapshot snapshot)
        {
            if (snapshot.State is State ss) _state = ss;
        }
    }
}