using System;
using System.Dynamic;
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
            var (pid, _, actorName, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            await providerState
                .GetEventsAsync(actorName, 0, o =>
                {
                    Assert.IsType(typeof(Multiplied), o);
                    Assert.Equal(2, (o as Multiplied).Amount);
                });
        }

        [Fact]
        public async void SnapshotsAreSavedToPersistence()
        {
            var (pid, _, actorName, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            pid.Tell(new RequestSnapshot());
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorName);
            var snapshotState = snapshot as State;
            Assert.Equal(10, snapshotState.Value);
        }

        [Fact]
        public async void EventsCanBeDeleted()
        {
            var (pid, _, actorName, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            await providerState.DeleteEventsAsync(actorName, 1);
            var events = new List<object>();
            await providerState.GetEventsAsync(actorName, 0, v => events.Add(v));

            Assert.Equal(0, events.Count);
        }

        [Fact]
        public async void SnapshotsCanBeDeleted()
        {
            var (pid, _, actorName, providerState) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 10 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorName, 1);
            var (snapshot, _) = await providerState.GetSnapshotAsync(actorName);
            Assert.Null(snapshot);
        }

        [Fact]
        public async void GivenEventsOnly_StateIsRestoredFromEvents()
        {
            var (pid, props, actorName, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async void GivenASnapshotOnly_StateIsRestoredFromTheSnapshot()
        {
            var (pid, props, actorName, providerState) = CreateTestActor();
            await providerState.PersistSnapshotAsync(actorName, 0, new State { Value = 10 });
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(10, state);
        }

        [Fact]
        public async void GivenEventsThenASnapshot_StateShouldBeRestoredFromTheSnapshot()
        {
            var (pid, props, actorName, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 2, state);
        }

        [Fact]
        public async void GivenASnapshotAndSubsequentEvents_StateShouldBeRestoredFromSnapshotAndSubsequentEvents()
        {
            var (pid, props, actorName, _) = CreateTestActor();
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new Multiply { Amount = 8 });
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_StateIsRestoredFromMostRecentSnapshot()
        {
            var (pid, props, actorName, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteEventsAsync(actorName, 2); // just to be sure state isn't recovered from events
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenMultipleSnapshots_DeleteSnapshotObeysIndex()
        {
            var (pid, props, actorName, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new RequestSnapshot());
            await providerState.DeleteSnapshotsAsync(actorName, 1);
            await providerState.DeleteEventsAsync(actorName, 2);

            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        [Fact]
        public async void GivenASnapshotAndEvents_WhenSnapshotDeleted_StateShouldBeRestoredFromEvents()
        {
            var (pid, props, actorName, providerState) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            pid.Tell(new Multiply { Amount = 8 });
            await providerState.DeleteSnapshotsAsync(actorName, 3);
            
            var state = await RestartActorAndGetState(pid, props, actorName);
            Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
        }

        [Fact]
        public async void Index_IncrementsOnEventsSaved()
        {
            var (pid, _, _, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromMilliseconds(250));
            Assert.Equal(1, index);
            pid.Tell(new Multiply { Amount = 4 });
            index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromMilliseconds(250));
            Assert.Equal(2, index);
        }

        [Fact]
        public async void Index_IsNotAffectedByTakingASnapshot()
        {
            var (pid, _, _, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new RequestSnapshot());
            pid.Tell(new Multiply { Amount = 4 });
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromMilliseconds(250));
            Assert.Equal(2, index);
        }

        [Fact]
        public async void Index_IsCorrectAfterRecovery()
        {
            var (pid, props, actorName, _) = CreateTestActor();

            pid.Tell(new Multiply { Amount = 2 });
            pid.Tell(new Multiply { Amount = 4 });

            var state = await RestartActorAndGetState(pid, props, actorName);
            var index = await pid.RequestAsync<long>(new GetIndex(), TimeSpan.FromMilliseconds(250));
            Assert.Equal(2, index);
            Assert.Equal(InitialState * 2 * 4, state);
        }

        private (PID pid, Props props, string actorName, IProviderState providerState) CreateTestActor()
        {
            var actorName = Guid.NewGuid().ToString();
            var inMemoryProviderState = new InMemoryProviderState();
            var provider = new InMemoryProvider(inMemoryProviderState);
            var props = Actor.FromProducer(() => new ExamplePersistentActor())
                .WithReceiveMiddleware(Persistence.Using(provider))
                .WithMailbox(() => new TestMailbox());
            var pid = Actor.SpawnNamed(props, actorName);
            return (pid, props, actorName, inMemoryProviderState);
        }

        private async Task<int> RestartActorAndGetState(PID pid, Props props, string actorName)
        {
            pid.Stop();
            pid = Actor.SpawnNamed(props, actorName);
            return await pid.RequestAsync<int>(new GetState());
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

    internal class ExamplePersistentActor : IPersistentActor
    {
        private State _state = new State{Value = 1};
        public Persistence Persistence { get; set; }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started msg:
                    RegisterHandlers();
                    break;
                case RecoveryStarted msg:
                    RegisterHandlers();
                    break;
                case GetState msg:
                    context.Sender.Tell(_state.Value);
                    break;
                case GetIndex msg:
                    context.Sender.Tell(Persistence.Index);
                    break;
                case RequestSnapshot msg:
                    await Persistence.PersistSnapshotAsync(new State { Value = _state.Value });
                    break;
                case Multiply msg:
                    await Persistence.PersistEventAsync(new Multiplied { Amount = msg.Amount });
                    break;
            }
        }

        private void RegisterHandlers()
        {
            Persistence.OnRecoverSnapshot += Persistence_OnRecoverSnapshot;
            Persistence.OnRecoverEvent += Persistence_OnRecoverEvent;
            Persistence.OnPersistedEvent += Persistence_OnPersistedEvent;
        }

        private Task Persistence_OnRecoverSnapshot(object sender, RecoverSnapshotArgs e)
        {
            if (e.Snapshot is State ss)
            {
                _state = ss;
            }

            return Actor.Done;
        }

        private Task Persistence_OnRecoverEvent(object sender, RecoverEventArgs e)
        {
            UpdateState(e.Event);

            return Actor.Done;
        }

        private Task Persistence_OnPersistedEvent(object sender, PersistedEventArgs e)
        {
            UpdateState(e.Event);

            return Actor.Done;
        }

        private void UpdateState(object message)
        {
            switch (message)
            {
                case Multiplied msg:
                    _state.Value = _state.Value * msg.Amount;
                    break;
            }
        }
    }
}
