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
        private static readonly RootContext Context = new(new ActorSystem());

        [Fact]
        public async void EventsAreSavedToPersistence()
        {
            (PID pid, _, string actorId, IProvider providerState) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 2});
            await providerState
                .GetEventsAsync(actorId, 0, long.MaxValue, o =>
                    {
                        Assert.IsType<Multiplied>(o);
                        Assert.Equal(2, ((Multiplied)o).Amount);
                    }
                );
        }

        [Fact]
        public async void SnapshotsAreSavedToPersistence()
        {
            (PID pid, _, string actorId, IProvider providerState) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 10});
            Context.Send(pid, new RequestSnapshot());
            (object? snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            State snapshotState = snapshot as State;
            Assert.Equal(10, snapshotState.Value);
        }

        [Fact]
        public async void EventsCanBeDeleted()
        {
            (PID pid, _, string actorId, IProvider providerState) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 10});
            await providerState.DeleteEventsAsync(actorId, 1);
            List<object> events = new List<object>();
            await providerState.GetEventsAsync(actorId, 0, long.MaxValue, v => events.Add(v));

            Assert.Empty(events);
        }

        [Fact]
        public async void SnapshotsCanBeDeleted()
        {
            (PID pid, _, string actorId, IProvider providerState) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 10});
            Context.Send(pid, new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 1);
            (object? snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            Assert.Null(snapshot);
        }

        [Fact]
        public async void GivenEventsOnly_StateIsRestoredFromEvents()
        {
            (PID pid, Props props, _, _) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 2});
            int state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async void GivenASnapshotOnly_StateIsRestoredFromTheSnapshot()
        {
            (PID pid, Props props, string actorId, IProvider providerState) = CreateTestActor();
            await providerState.PersistSnapshotAsync(actorId, 0, new State {Value = 10});
            int state = await RestartActorAndGetState(pid, props);
            Assert.Equal(10, state);
        }

        [Fact]
        public async void GivenEventsThenASnapshot_StateShouldBeRestoredFromTheSnapshot()
        {
            (PID pid, Props props, _, _) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            int state = await RestartActorAndGetState(pid, props);
            int expectedState = InitialState * 2 * 2;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async void GivenASnapshotAndSubsequentEvents_StateShouldBeRestoredFromSnapshotAndSubsequentEvents()
        {
            (PID pid, Props props, _, _) = CreateTestActor();
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            Context.Send(pid, new Multiply {Amount = 4});
            Context.Send(pid, new Multiply {Amount = 8});
            int state = await RestartActorAndGetState(pid, props);
            int expectedState = InitialState * 2 * 2 * 4 * 8;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_StateIsRestoredFromMostRecentSnapshot()
        {
            (PID pid, Props props, string actorId, IProvider providerState) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            Context.Send(pid, new Multiply {Amount = 4});
            Context.Send(pid, new RequestSnapshot());
            await providerState.DeleteEventsAsync(actorId, 2); // just to be sure state isn't recovered from events
            int state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_DeleteSnapshotObeysIndex()
        {
            (PID pid, Props props, string actorId, IProvider providerState) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            Context.Send(pid, new Multiply {Amount = 4});
            Context.Send(pid, new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 0);
            await providerState.DeleteEventsAsync(actorId, 1);
            int state = await RestartActorAndGetState(pid, props);
            int expectedState = InitialState * 2 * 4;
            Assert.Equal(expectedState, state);
        }

        [Fact]
        public async void GivenASnapshotAndEvents_WhenSnapshotDeleted_StateShouldBeRestoredFromEvents()
        {
            (PID pid, Props props, string actorId, IProvider providerState) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            Context.Send(pid, new Multiply {Amount = 4});
            Context.Send(pid, new Multiply {Amount = 8});
            await providerState.DeleteSnapshotsAsync(actorId, 3);

            int state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async void Index_IncrementsOnEventsSaved()
        {
            (PID pid, _, _, _) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            long index = await Context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(0, index);
            Context.Send(pid, new Multiply {Amount = 4});
            index = await Context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
        }

        [Fact]
        public async void Index_IsNotAffectedByTakingASnapshot()
        {
            (PID pid, _, _, _) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new RequestSnapshot());
            Context.Send(pid, new Multiply {Amount = 4});
            long index = await Context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
        }

        [Fact]
        public async void Index_IsCorrectAfterRecovery()
        {
            (PID pid, Props props, _, _) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 4});

            await Context.StopAsync(pid);
            pid = Context.Spawn(props);
            int state = await Context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromSeconds(1));
            long index = await Context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenEvents_CanReplayFromStartIndexToEndIndex()
        {
            (PID pid, _, string actorId, IProvider providerState) = CreateTestActor();

            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 2});
            Context.Send(pid, new Multiply {Amount = 4});
            Context.Send(pid, new Multiply {Amount = 8});
            List<object> messages = new List<object>();
            await providerState.GetEventsAsync(actorId, 1, 2, msg => messages.Add(msg));
            Assert.Equal(2, messages.Count);
            Assert.Equal(2, ((Multiplied)messages[0]).Amount);
            Assert.Equal(4, ((Multiplied)messages[1]).Amount);
        }

        [Fact]
        public async Task CanUseSeparateStores()
        {
            string actorId = Guid.NewGuid().ToString();
            InMemoryProvider eventStore = new InMemoryProvider();
            InMemoryProvider snapshotStore = new InMemoryProvider();
            Props props = Props.FromProducer(() => new ExamplePersistentActor(eventStore, snapshotStore, actorId))
                .WithMailbox(() => new TestMailbox());
            PID pid = Context.Spawn(props);

            Context.Send(pid, new Multiply {Amount = 2});
            List<object> eventStoreMessages = new List<object>();
            List<object> snapshotStoreMessages = new List<object>();
            await eventStore.GetEventsAsync(actorId, 0, 1, msg => eventStoreMessages.Add(msg));
            Assert.Single(eventStoreMessages);
            await snapshotStore.GetEventsAsync(actorId, 0, 1, msg => snapshotStoreMessages.Add(msg));
            Assert.Empty(snapshotStoreMessages);
        }

        private (PID pid, Props props, string actorId, IProvider provider) CreateTestActor()
        {
            string actorId = Guid.NewGuid().ToString();
            InMemoryProvider inMemoryProvider = new InMemoryProvider();
            Props props = Props
                .FromProducer(() => new ExamplePersistentActor(inMemoryProvider, inMemoryProvider, actorId))
                .WithMailbox(() => new TestMailbox());
            PID pid = Context.Spawn(props);
            return (pid, props, actorId, inMemoryProvider);
        }

        private async Task<int> RestartActorAndGetState(PID pid, Props props)
        {
            await Context.StopAsync(pid);
            pid = Context.Spawn(props);
            return await Context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromMilliseconds(500));
        }
    }

    internal class State
    {
        public int Value { get; set; }
    }

    internal class GetState
    {
    }

    internal class GetIndex
    {
    }

    internal class Multiply
    {
        public int Amount { get; set; }
    }

    internal class Multiplied
    {
        public int Amount { get; set; }
    }

    internal class RequestSnapshot
    {
    }

    internal class ExamplePersistentActor : IActor
    {
        private readonly Persistence _persistence;
        private State _state = new() {Value = 1};

        public ExamplePersistentActor(IEventStore eventStore, ISnapshotStore snapshotStore, string persistenceId) =>
            _persistence =
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
            if (snapshot.State is State ss)
            {
                _state = ss;
            }
        }
    }
}
