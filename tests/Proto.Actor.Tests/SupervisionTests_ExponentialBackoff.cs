using System;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests_ExponentialBackoff
    {
        [Fact]
        public void FailureOutsideWindow_ResetsFailureCount()
        {
            RestartStatistics rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(11)));
            ExponentialBackoffStrategy strategy =
                new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            strategy.HandleFailure(null, null, rs, null, null);
            Assert.Equal(1, rs.FailureCount);
        }

        [Fact]
        public void FailureInsideWindow_IncrementsFailureCount()
        {
            RestartStatistics rs = new RestartStatistics(0, DateTime.Now);
            ExponentialBackoffStrategy strategy =
                new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            strategy.HandleFailure(null, null, rs, null, null);
            strategy.HandleFailure(null, null, rs, null, null);
            strategy.HandleFailure(null, null, rs, null, null);
            Assert.Equal(3, rs.FailureCount);
        }
    }
}
