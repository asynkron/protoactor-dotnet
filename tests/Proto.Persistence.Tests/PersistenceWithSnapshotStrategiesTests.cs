using System;
using System.Threading.Tasks;
using Proto.Persistence.SnapshotStrategies;
using Xunit;

namespace Proto.Persistence.Tests;

public class PersistenceWithSnapshotStrategiesTests
{
    [Fact]
    public async Task GivenAnIntervalStrategy_ShouldSaveSnapshotAccordingly()
    {
        var state = 1;
        var provider = new InMemoryProvider();
        var actorId = Guid.NewGuid().ToString();

        var persistence = Persistence.WithEventSourcingAndSnapshotting(provider, provider, actorId,
            @event => { state *= ((Multiplied)@event.Data).Amount; },
            snapshot => { state = (int)snapshot.State; },
            new IntervalStrategy(1), () => state
        );

        await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
        await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
        await persistence.PersistEventAsync(new Multiplied { Amount = 2 });
        var snapshots = provider.GetSnapshots(actorId);
        Assert.Equal(3, snapshots.Count);
        Assert.Equal(2, snapshots[0]);
        Assert.Equal(4, snapshots[1]);
        Assert.Equal(8, snapshots[2]);
    }
}