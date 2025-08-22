using System.Threading.Tasks;
using FluentAssertions;
using Proto.TestKit;
using Xunit;

namespace Proto.TestKit.Tests
{
public class ProbeMiddlewareTests
{
    [Fact]
    public async Task Receive_probe_captures_messages()
    {
        var system = new ActorSystem();
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromProducer(() => new EmptyActor()).WithReceiveProbe(probe);
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hello");
        (await probe.FishForMessageAsync<string>(s => s == "hello")).Should().Be("hello");
    }

    [Fact]
    public async Task Send_probe_captures_outgoing_messages()
    {
        var system = new ActorSystem();
        var (probe, _) = system.CreateTestProbe();

        var target = system.Root.Spawn(Props.FromFunc(ctx => Task.CompletedTask));
        var props = Props.FromProducer(() => new ForwardActor(target)).WithSendProbe(probe);
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hi");
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hi");
        probe.Sender.Should().Be(target);
    }

    [Fact]
    public async Task Mailbox_probe_captures_messages_and_system_messages()
    {
        var system = new ActorSystem();
        var (probe, _) = system.CreateTestProbe();

        var props = Props.FromProducer(() => new EmptyActor()).WithMailboxProbe(probe);
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hello");
        await probe.ExpectNextUserMessageAsync<string>(s => s == "hello");

        system.Root.Stop(pid);
        await probe.ExpectNextSystemMessageAsync<Stop>();
    }

    private class EmptyActor : IActor
    {
        public Task ReceiveAsync(IContext context) => Task.CompletedTask;
    }

    private class ForwardActor : IActor
    {
        private readonly PID _target;
        public ForwardActor(PID target) => _target = target;
        public Task ReceiveAsync(IContext context)
        {
            context.Send(_target, context.Message!);
            return Task.CompletedTask;
        }
    }

}
}
