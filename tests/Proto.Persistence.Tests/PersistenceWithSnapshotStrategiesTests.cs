using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Persistence.SnapshotStrategies;
using Xunit;

namespace Proto.Persistence.Tests
{
    public class PersistenceWithSnapshotStrategiesTests
    {
        [Fact]
        public async Task GivenAnIntervalStrategy_ShouldSaveSnapshotAccordingly()
        {
            int state = 1;
            InMemoryProvider provider = new InMemoryProvider();
            string actorId = Guid.NewGuid().ToString();
            Persistence persistence = Persistence.WithEventSourcingAndSnapshotting(provider, provider, actorId,
                @event => { state *= ((Multiplied)@event.Data).Amount; },
                snapshot => { state = (int)snapshot.State; },
                new IntervalStrategy(1), () => state
            );

            await persistence.PersistEventAsync(new Multiplied {Amount = 2});
            await persistence.PersistEventAsync(new Multiplied {Amount = 2});
            await persistence.PersistEventAsync(new Multiplied {Amount = 2});
            Dictionary<long, object> snapshots = provider.GetSnapshots(actorId);
            Assert.Equal(3, snapshots.Count);
            Assert.Equal(2, snapshots[0]);
            Assert.Equal(4, snapshots[1]);
            Assert.Equal(8, snapshots[2]);
        }
    }
}
