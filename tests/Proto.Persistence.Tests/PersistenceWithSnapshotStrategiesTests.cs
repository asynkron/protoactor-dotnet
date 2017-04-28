using System;
using Proto.Persistence.SnapshotStrategies;
using Xunit;

namespace Proto.Persistence.Tests
{
    public class PersistenceWithSnapshotStrategiesTests
    {
        [Fact]
        public async void GivenAnIntervalStrategy_ShouldSaveSnapshotAccordingly()
        {
            var state = 1;
            var inMemoryProviderState = new InMemoryProviderState();
            var provider = new InMemoryProvider(inMemoryProviderState);
            var actorId = Guid.NewGuid().ToString();
            var persistence = Persistence.WithEventSourcingAndSnapshotting(provider, actorId, 
                @event => { state = state * (@event.Data as Multiplied).Amount; },
                snapshot => { state = (int)snapshot.State; }, 
                new IntervalStrategy(1), () => state);

            await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
            await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
            await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
            var snapshots = inMemoryProviderState.GetSnapshots(actorId);
            Assert.Equal(3, snapshots.Count);
            Assert.Equal(2, snapshots[1]);
            Assert.Equal(4, snapshots[2]);
            Assert.Equal(8, snapshots[3]);
        }
    }
}