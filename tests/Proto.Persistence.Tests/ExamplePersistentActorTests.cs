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
        public async void EventsAreSavedToPersistence()
        {
            var (pid, _, actorId, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            await providerState
                .GetEventsAsync(actorId, 0, o =>
                {
                    Assert.IsType(typeof(Multiplied), o);
                    Assert.Equal(2, (o as Multiplied).Amount);
                });
        }

        [Fact]
        public async void SnapshotsAreSavedToPersistence()
        {
            var (pid, _, actorId, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            pid.Tell(new RequestSnapshot());
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            var snapshotState = snapshot as State;
            Assert.Equal(10, snapshotState.Value);
        }

        [Fact]
        public async void EventsCanBeDeleted()
        {
            var (pid, _, actorId, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            await providerState.DeleteEventsAsync(actorId, 1);
            var events = new List<object>();
            await providerState.GetEventsAsync(actorId, 0, v => events.Add(v));

            Assert.Equal(0, events.Count);
        }

        [Fact]
        public async void SnapshotsCanBeDeleted()
        {
            var (pid, _, actorId, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 1);
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
            Assert.Null(snapshot);
        }

        [Fact]
        public async void GivenEventsOnly_StateIsRestoredFromEvents()
        {
            var (pid, props, actorId, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async void GivenASnapshotOnly_StateIsRestoredFromTheSnapshot()
        {
            var (pid, props, actorId, providerState) = CreateTestActor();
            await providerState.PersistSnapshotAsync(actorId, 0, new State { Value = 10 });
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(10, state);
        }

        [Fact]
        public async void GivenEventsThenASnapshot_StateShouldBeRestoredFromTheSnapshot()
        {
            var (pid, props, actorId, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async void GivenASnapshotAndSubsequentEvents_StateShouldBeRestoredFromSnapshotAndSubsequentEvents()
        {
            var (pid, props, actorId, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new Multiply { Amount = 8 });
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_StateIsRestoredFromMostRecentSnapshot()
        {
            var (pid, props, actorId, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteEventsAsync(actorId, 2); // just to be sure state isn't recovered from events
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_DeleteSnapshotObeysIndex()
        {
            var (pid, props, actorId, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorId, 1);
            await providerState.DeleteEventsAsync(actorId, 2);

            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenASnapshotAndEvents_WhenSnapshotDeleted_StateShouldBeRestoredFromEvents()
        {
            var (pid, props, actorId, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new Multiply { Amount = 8 });
            await providerState.DeleteSnapshotsAsync(actorId, 3);
            
            var state = await RestartActorAndGetState(pid, props);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async void Index_IncrementsOnEventsSaved()
        {
            var (pid, _, _, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(1, index);
            pid.Tell(new Multiply { Amount = 4 });
            index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(2, index);
        }

        [Fact]
        public async void Index_IsNotAffectedByTakingASnapshot()
        {
            var (pid, _, _, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(2, index);
        }

        [Fact]
        public async void Index_IsCorrectAfterRecovery()
        {
            var (pid, props, _, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 4 });

            pid.Stop();
            pid = Actor.Spawn(props);
            var state = await pid.RequestAsync<int>(new GetState(), TimeSpan.FromSeconds(1));
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromSeconds(1));
            Assert.Equal(2, index);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        private (PID pid, Props props, string actorId, IProviderState providerState) CreateTestActor()
        {
            var actorId = Guid.NewGuid().ToString();
            var inMemoryProviderState = new InMemoryProviderState();
            var provider = new InMemoryProvider(inMemoryProviderState);
            var props = Actor.FromProducer(() => new ExamplePersistentActor(provider, actorId))
                .WithMailbox(() => new TestMailbox());
            var pid = Actor.Spawn(props);
            return (pid, props, actorId, inMemoryProviderState);
        }

        private async Task<int> RestartActorAndGetState(PID pid, Props props)
        {
            pid.Stop();
            pid = Actor.Spawn(props);
            return await pid.RequestAsync<int>(new GetState(), TimeSpan.FromMilliseconds(500));
        }
    }

    internal class State
    {
        public int Value { get; set; }
    }

    internal class GetState { }
    internal class GetIndex { }
    internal class Multiply
    {
        public int Amount { get; set; }
    }

    internal class Multiplied
    {
        public int Amount { get; set; }
    }

    internal class RequestSnapshot { }

    internal class ExamplePersistentActor : IActor
    {
        private State _state = new State{Value = 1};
        private readonly Persistence _persistence;

        public ExamplePersistentActor(IProvider provider, string persistenceId)
        {
            _persistence = Persistence.WithEventSourcingAndSnapshotting(provider, persistenceId, ApplyEvent, ApplySnapshot);
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

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    await _persistence.RecoverStateAsync();
                    break;
                case GetState msg:
                    context.Sender.Tell(_state.Value);
                    break;
                case GetIndex msg:
                    context.Sender.Tell(_persistence.Index);
                    break;
                case RequestSnapshot msg:
                    await _persistence.PersistSnapshotAsync(new State { Value = _state.Value });
                    break;
                case Multiply msg:
                    await _persistence.PersistEventAsync(new Multiplied { Amount = msg.Amount });
                    break;
            }
        }
    }
}
