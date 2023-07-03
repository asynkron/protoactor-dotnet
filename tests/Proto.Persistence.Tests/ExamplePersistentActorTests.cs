using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Marten;
using Proto.Persistence.Marten;
using Proto.TestFixtures;
using Testcontainers.PostgreSql;
using Xunit;

namespace Proto.Persistence.Tests;



public class ExamplePersistentActorTests: IAsyncLifetime
{
    private const int InitialState = 1;
    private readonly PostgreSqlContainer DbContainer = new PostgreSqlBuilder()
        .WithDatabase("IntegrationTests")
        .WithUsername("postgres")
        .WithPassword("root")
        .WithCommand(new[] { "-c", "log_statement=all" })
        .Build();

    private IProvider GetProvider(TestProvider providerType)
    {
        return providerType switch
        {
            TestProvider.InMemory => new InMemoryProvider(),
            TestProvider.Marten => new MartenProvider(DocumentStore.For(DbContainer.GetConnectionString())),
            _ => throw new ArgumentOutOfRangeException(nameof(providerType), providerType, null)
        };
    }
    
    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task EventsAreSavedToPersistence(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, actorId, providerState) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 2 });

        await providerState
            .GetEventsAsync(actorId, 0, long.MaxValue, o =>
                {
                    Assert.IsType<Multiplied>(o);
                    Assert.Equal(2, ((Multiplied)o).Amount);
                }
            );
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task SnapshotsAreSavedToPersistence(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, actorId, providerState) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 10 });
        context.Send(pid, new RequestSnapshot());
        var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
        var snapshotState = snapshot as State;
        Assert.Equal(10, snapshotState.Value);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task EventsCanBeDeleted(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, actorId, providerState) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 10 });
        await providerState.DeleteEventsAsync(actorId, 1);
        var events = new List<object>();
        await providerState.GetEventsAsync(actorId, 0, long.MaxValue, v => events.Add(v));

        Assert.Empty(events);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task SnapshotsCanBeDeleted(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, actorId, providerState) = CreateTestActor(context,  provider);
        context.Send(pid, new Multiply { Amount = 10 });
        context.Send(pid, new RequestSnapshot());
        await providerState.DeleteSnapshotsAsync(actorId, 1);
        var (snapshot, _) = await providerState.GetSnapshotAsync(actorId);
        Assert.Null(snapshot);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenEventsOnly_StateIsRestoredFromEvents(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, _, _) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 2 });
        var state = await RestartActorAndGetState(pid, props, context);
        Assert.Equal(InitialState * 2 * 2, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenASnapshotOnly_StateIsRestoredFromTheSnapshot(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, actorId, providerState) = CreateTestActor(context,provider);
        await providerState.PersistSnapshotAsync(actorId, 0, new State { Value = 10 });
        var state = await RestartActorAndGetState(pid, props, context);
        Assert.Equal(10, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    
    public async Task GivenEventsThenASnapshot_StateShouldBeRestoredFromTheSnapshot(TestProvider  testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, _, _) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        var state = await RestartActorAndGetState(pid, props, context);
        var expectedState = InitialState * 2 * 2;
        Assert.Equal(expectedState, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenASnapshotAndSubsequentEvents_StateShouldBeRestoredFromSnapshotAndSubsequentEvents( TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, _, _) = CreateTestActor(context,provider);
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        context.Send(pid, new Multiply { Amount = 4 });
        context.Send(pid, new Multiply { Amount = 8 });
        var state = await RestartActorAndGetState(pid, props, context);
        var expectedState = InitialState * 2 * 2 * 4 * 8;
        Assert.Equal(expectedState, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenMultipleSnapshots_StateIsRestoredFromMostRecentSnapshot(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, actorId, providerState) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        context.Send(pid, new Multiply { Amount = 4 });
        context.Send(pid, new RequestSnapshot());
        await providerState.DeleteEventsAsync(actorId, 2); // just to be sure state isn't recovered from events
        var state = await RestartActorAndGetState(pid, props, context);
        Assert.Equal(InitialState * 2 * 4, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenMultipleSnapshots_DeleteSnapshotObeysIndex(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, actorId, providerState) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        context.Send(pid, new Multiply { Amount = 4 });
        context.Send(pid, new RequestSnapshot());
        await providerState.DeleteSnapshotsAsync(actorId, 0);
        await providerState.DeleteEventsAsync(actorId, 1);
        var state = await RestartActorAndGetState(pid, props, context);
        var expectedState = InitialState * 2 * 4;
        Assert.Equal(expectedState, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenASnapshotAndEvents_WhenSnapshotDeleted_StateShouldBeRestoredFromEvents(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, actorId, providerState) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        context.Send(pid, new Multiply { Amount = 4 });
        context.Send(pid, new Multiply { Amount = 8 });
        await providerState.DeleteSnapshotsAsync(actorId, 3);

        var state = await RestartActorAndGetState(pid, props, context);
        Assert.Equal(InitialState * 2 * 2 * 4 * 8, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task Index_IncrementsOnEventsSaved(TestProvider  testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, _, _) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
        Assert.Equal(0, index);
        context.Send(pid, new Multiply { Amount = 4 });
        index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
        Assert.Equal(1, index);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task Index_IsIncrementedByTakingASnapshot(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, _, _) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new RequestSnapshot());
        context.Send(pid, new Multiply { Amount = 4 });
        var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
        Assert.Equal(2, index);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task Index_IsCorrectAfterRecovery(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, props, _, _) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 4 });

        await context.StopAsync(pid);
        pid = context.Spawn(props);
        var state = await context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromSeconds(1));
        var index = await context.RequestAsync<long>(pid, new GetIndex(), TimeSpan.FromSeconds(1));
        Assert.Equal(1, index);
        Assert.Equal(InitialState * 2 * 4, state);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task GivenEvents_CanReplayFromStartIndexToEndIndex(TestProvider testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;
        var provider = GetProvider(testProvider);
        var (pid, _, actorId, providerState) = CreateTestActor(context,provider);

        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 2 });
        context.Send(pid, new Multiply { Amount = 4 });
        context.Send(pid, new Multiply { Amount = 8 });
        var messages = new List<object>();
        await providerState.GetEventsAsync(actorId, 1, 2, msg => messages.Add(msg));
        Assert.Equal(2, messages.Count);
        Assert.Equal(2, ((Multiplied)messages[0]).Amount);
        Assert.Equal(4, ((Multiplied)messages[1]).Amount);
    }

    [Theory]
    [InlineData(TestProvider.InMemory)]
    [InlineData(TestProvider.Marten)]
    public async Task CanUseSeparateStores(TestProvider  testProvider)
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var actorId = Guid.NewGuid().ToString();
        var eventStore = GetProvider(testProvider);
        var snapshotStore = GetProvider(testProvider);

        var props = Props.FromProducer(() => new ExamplePersistentActor(eventStore, snapshotStore, actorId))
            .WithMailbox(() => new TestMailbox());

        var pid = context.Spawn(props);

        context.Send(pid, new Multiply { Amount = 2 });
        var eventStoreMessages = new List<object>();
        var snapshotStoreMessages = new List<object>();
        await eventStore.GetEventsAsync(actorId, 0, 1, msg => eventStoreMessages.Add(msg));
        Assert.Single(eventStoreMessages);
        await snapshotStore.GetEventsAsync(actorId, 0, 1, msg => snapshotStoreMessages.Add(msg));
        Assert.Empty(snapshotStoreMessages);
    }

    private (PID pid, Props props, string actorId, IProvider provider) CreateTestActor(IRootContext context,IProvider provider)
    {
        var actorId = Guid.NewGuid().ToString();
       

        var props = Props
            .FromProducer(() => new ExamplePersistentActor(provider, provider, actorId))
            .WithMailbox(() => new TestMailbox());

        var pid = context.Spawn(props);

        return (pid, props, actorId, provider);
    }

    private async Task<int> RestartActorAndGetState(PID pid, Props props, IRootContext context)
    {
        await context.StopAsync(pid);
        pid = context.Spawn(props);

        return await context.RequestAsync<int>(pid, new GetState(), TimeSpan.FromMilliseconds(500));
    }

   
    public async Task InitializeAsync()
    {
        await  DbContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await DbContainer.StopAsync();
    }
}

internal class State
{
    public int Value { get; set; }
}

public enum TestProvider
{
    InMemory=1,
    Couchbase=2,
    Marten=3,
    MongoDb=4,
    RavenDb=5,
    Sqlite=6,
    SqlServer=7,
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
    private State _state = new() { Value = 1 };

    public ExamplePersistentActor(IEventStore eventStore, ISnapshotStore snapshotStore, string persistenceId)
    {
        _persistence =
            Persistence.WithEventSourcingAndSnapshotting(eventStore, snapshotStore, persistenceId,
                ApplyEvent, ApplySnapshot
            );
    }

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
                await _persistence.PersistSnapshotAsync(new State { Value = _state.Value });

                break;
            case Multiply msg:
                await _persistence.PersistEventAsync(new Multiplied { Amount = msg.Amount });

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