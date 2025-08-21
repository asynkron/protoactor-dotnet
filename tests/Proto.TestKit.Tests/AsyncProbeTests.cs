using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Proto.TestKit.Tests
{
    public class AsyncProbeTests : TestKitBase
    {
        public AsyncProbeTests() => SetUp();

        [Fact]
        public async Task GetNextMessageAsync_receives_message()
        {
            Send(Probe, "hello");
            var msg = await GetNextMessageAsync<string>();
            msg.Should().Be("hello");
        }

        [Fact]
        public async Task ProcessMessagesAsync_returns_all_until_timeout()
        {
            Send(Probe, "a");
            Send(Probe, "b");

            var received = new List<string>();
            await foreach (var m in ProcessMessagesAsync<string>(TimeSpan.FromMilliseconds(100)))
            {
                received.Add(m);
            }

            received.Should().BeEquivalentTo(new[] { "a", "b" });
        }
    }
}
