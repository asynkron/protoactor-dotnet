using System.Threading.Tasks;
using FluentAssertions;
using Proto.TestKit;
using Xunit;

namespace Proto.TestKit.Tests
{
public class ProbeMiddlewareTests
{
    [Fact]
    public void Receive_probe_captures_messages()
    {
        var system = new ActorSystem();
        var probe = new TestProbe();
        system.Root.Spawn(Props.FromProducer(() => probe));

        var props = Props.FromProducer(() => new EmptyActor()).WithTestReceiveProbe(probe);
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hello");
        probe.GetNextMessage<string>().Should().Be("hello");
    }

    [Fact]
    public void Send_probe_captures_outgoing_messages()
    {
        var system = new ActorSystem();
        var probe = new TestProbe();
        system.Root.Spawn(Props.FromProducer(() => probe));

        var target = system.Root.Spawn(Props.FromFunc(ctx => Task.CompletedTask));
        var props = Props.FromProducer(() => new ForwardActor(target)).WithTestSendProbe(probe);
        var pid = system.Root.Spawn(props);

        system.Root.Send(pid, "hi");
        probe.GetNextMessage<string>().Should().Be("hi");
        probe.Sender.Should().Be(target);
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
