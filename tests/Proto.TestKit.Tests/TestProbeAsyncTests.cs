using System.Threading.Tasks;
using FluentAssertions;
using Proto;
using Proto.TestKit;
using Xunit;

namespace Proto.TestKit.Tests
{
public class TestProbeAsyncTests
{
    [Fact]
    public async Task GetNextMessageAsync_returns_message()
    {
        var system = new ActorSystem();
        var (probe, pid) = system.CreateTestProbe();

        system.Root.Send(pid, "hello");
        var msg = await probe.GetNextMessageAsync<string>();
        msg.Should().Be("hello");
    }

    [Fact]
    public async Task GetNextMessageAsync_with_predicate_returns_specific()
    {
        var system = new ActorSystem();
        var (probe, pid) = system.CreateTestProbe();

        system.Root.Send(pid, "a");
        system.Root.Send(pid, "b");
        var msg = await probe.FishForMessageAsync<string>(x => x == "b");
        msg.Should().Be("b");
    }
}
}
