using System.Linq;
using Proto.Persistence.SnapshotStrategies;
using Xunit;

namespace Proto.Persistence.Tests.SnapshotStrategies
{
    public class IntervalStrategyTests
    {
        [Theory]
        [InlineData(1, new[] { 1, 2, 3, 4, 5 })]
        [InlineData(2, new[] { 2, 4, 6, 8, 10 })]
        [InlineData(5, new[] { 5, 10, 15, 20, 25 })]
        public void IntervalStrategy_ShouldSnapshotAccordingToTheInterval(int interval, int[] expected)
        {
            var strategy = new IntervalStrategy(interval);
            for (int index = 1; index <= expected.Last(); index++)
            {
                if (expected.Contains(index))
                {
                    Assert.True(strategy.ShouldTakeSnapshot(new PersistedEvent(null, index)));
                }
                else
                {
                    Assert.False(strategy.ShouldTakeSnapshot(new PersistedEvent(null, index)));
                }
            }
        }
    }
}
