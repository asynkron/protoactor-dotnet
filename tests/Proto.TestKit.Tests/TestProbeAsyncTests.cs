using System;
using System.Threading;
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
    public async Task GetNextUserMessageAsync_returns_message()
    {
        var system = new ActorSystem();
        var (probe, pid) = system.CreateTestProbe();

        system.Root.Send(pid, "hello");
        var message = await probe.GetNextUserMessageAsync<string>(s => s == "hello");
        message.Should().Be("hello");
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

    [Fact]
    public async Task ExpectEmptyMailboxAsync_detects_empty_mailbox()
    {
        var system = new ActorSystem();
        var (probe, pid) = system.CreateTestProbe();

        system.Root.Send(pid, "init");
        await probe.ExpectNextUserMessageAsync<string>(s => s == "init");

        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task ExpectEmptyMailboxAsync_throws_when_message_present()
    {
        var system = new ActorSystem();
        var (probe, pid) = system.CreateTestProbe();

        system.Root.Send(pid, "hello");
        await Assert.ThrowsAsync<TestKitException>(() => probe.ExpectEmptyMailboxAsync());
    }
}
}
