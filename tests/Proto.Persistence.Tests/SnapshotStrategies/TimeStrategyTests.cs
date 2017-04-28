using System;
using Proto.Persistence.SnapshotStrategies;
using Xunit;

namespace Proto.Persistence.Tests.SnapshotStrategies
{
    public class TimeStrategyTests
    {
        [Fact]
        public void TimeStrategy_ShouldSnapshotAccordingToTheInterval()
        {
            var now = DateTime.Parse("2000-01-01 12:00:00");
            var strategy = new TimeStrategy(TimeSpan.FromSeconds(10), () => now);
            Assert.False(strategy.ShouldTakeSnapshot(new PersistedEvent(null, 0)));
            now = now.AddSeconds(5);
            Assert.False(strategy.ShouldTakeSnapshot(new PersistedEvent(null, 0)));
            now = now.AddSeconds(5);
            Assert.True(strategy.ShouldTakeSnapshot(new PersistedEvent(null, 0)));
            now = now.AddSeconds(5);
            Assert.False(strategy.ShouldTakeSnapshot(new PersistedEvent(null, 0)));
            now = now.AddSeconds(5);
            Assert.True(strategy.ShouldTakeSnapshot(new PersistedEvent(null, 0)));
        }
    }
}