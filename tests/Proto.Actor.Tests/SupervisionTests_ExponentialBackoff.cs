using System;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests_ExponentialBackoff
    {
        [Fact]
        public void FailureOutsideWindow_ZeroCount()
        {
            var rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(11)));
            var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            strategy.HandleFailure(null, null, rs, null);
            Assert.Equal(0, rs.FailureCount);
        }

        [Fact]
        public void FailureInsideWindow_IncrementCount()
        {
            var rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(9)));
            var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            strategy.HandleFailure(null, null, rs, null);
            Assert.Equal(11, rs.FailureCount);
        }
    }
}