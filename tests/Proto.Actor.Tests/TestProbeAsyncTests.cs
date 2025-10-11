using System.Threading.Tasks;
using Proto;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class TestProbeAsyncTests
{
    [Fact]
    public async Task Probe_can_await_next_message()
    {
        await using var system = new ActorSystem();
        var (probe, probePid) = system.CreateTestProbe();

        var target = system.Root.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is string s)
            {
                ctx.Send(probePid, s);
            }
            return Task.CompletedTask;
        }));

        system.Root.Send(target, "hello");
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");
    }

    [Fact]
    public async Task Probe_can_wait_for_specific_message()
    {
        await using var system = new ActorSystem();
        var (probe, probePid) = system.CreateTestProbe();

        var target = system.Root.Spawn(Props.FromFunc(ctx =>
        {
            if (ctx.Message is string s)
            {
                ctx.Send(probePid, s);
            }
            return Task.CompletedTask;
        }));

        system.Root.Send(target, "one");
        system.Root.Send(target, "two");

        var msg = await probe.FishForMessageAsync<string>(x => x == "two");
        Assert.Equal("two", msg);
    }
}
