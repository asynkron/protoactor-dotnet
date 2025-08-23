using System;
using System.Threading.Tasks;
using Xunit;
using static Proto.TestKit.TestKit;

namespace Proto.TestKit.Tests
{
    public class AwaitConditionAsyncTests
    {
        [Fact]
        public async Task Completes_when_condition_met()
        {
            var value = 0;
            _ = Task.Run(async () =>
            {
                // Update value asynchronously after short delay
                await Task.Delay(50);
                value = 1;
            });

            await AwaitConditionAsync(() => value == 1, TimeSpan.FromSeconds(1));
        }

        [Fact]
        public async Task Throws_when_condition_not_met()
        {
            await Assert.ThrowsAsync<TimeoutException>(
                () => AwaitConditionAsync(() => false, TimeSpan.FromMilliseconds(100)));
        }

        [Fact]
        public async Task Throws_with_custom_message()
        {
            const string message = "not fast enough";

            var ex = await Assert.ThrowsAsync<TimeoutException>(
                () => AwaitConditionAsync(() => false, TimeSpan.FromMilliseconds(50), message));

            Assert.Equal(message, ex.Message);
        }
    }
}

