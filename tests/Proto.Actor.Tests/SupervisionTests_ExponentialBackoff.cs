using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests
{
    public class SupervisionTests_ExponentialBackoff
    {
        [Fact]
        public async Task FailureOutsideWindow_ZeroCount()
        {
            var rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(11)));
            var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            await strategy.HandleFailureAsync(null, null, rs, null);
            Assert.Equal(0, rs.FailureCount);
        }

        [Fact]
        public async Task FailureInsideWindow_IncrementCount()
        {
            var rs = new RestartStatistics(10, DateTime.Now.Subtract(TimeSpan.FromSeconds(9)));
            var strategy = new ExponentialBackoffStrategy(TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(1));
            await strategy.HandleFailureAsync(null, null, rs, null);
            Assert.Equal(11, rs.FailureCount);
        }
    }
}