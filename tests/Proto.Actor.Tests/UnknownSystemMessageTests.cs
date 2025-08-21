using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class UnknownSystemMessageTests
{
    private class UnknownSystemMessage : SystemMessage
    {
    }

    private record GetChild;

    private class ParentActor : IActor
    {
        private readonly Props _childProps;

        public ParentActor(Props childProps)
        {
            _childProps = childProps;
        }

        public PID? Child { get; set; }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    Child = context.Spawn(_childProps);
                    break;
                case GetChild:
                    context.Respond(Child!);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Unknown_system_message_should_escalate_to_parent()
    {
        await using var system = new ActorSystem();

        // Attach a probe to the parent mailbox to observe system messages such as Failure
        var probe = new TestProbe();
        var probePid = system.Root.Spawn(Props.FromProducer(() => probe));
        // ensure the probe is fully started before it is used
        system.Root.Send(probePid, "init");
        await probe.FishForMessageAsync<string>();

        var childProps = Props.FromFunc(ctx => Task.CompletedTask);
        var parentProps = Props.FromProducer(() => new ParentActor(childProps))
            .WithMailboxProbe(probe);

        var parent = system.Root.Spawn(parentProps);
        var child = await system.Root.RequestAsync<PID>(parent, new GetChild());

        child.SendSystemMessage(system, new UnknownSystemMessage());

        // The parent should receive a Failure describing the child and exception
        var failure = await probe.FishForMessageAsync<Failure>();
        Assert.Equal(child, failure.Who);
        Assert.IsType<InvalidOperationException>(failure.Reason);
    }
}
