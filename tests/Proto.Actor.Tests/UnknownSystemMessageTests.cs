using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
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
        var parentStats = new TestMailboxStatistics(m => m is Failure);

        var childProps = Props.FromFunc(ctx => Task.CompletedTask);
        var parentProps = Props.FromProducer(() => new ParentActor(childProps))
            .WithMailbox(() => UnboundedMailbox.Create(parentStats));

        var parent = system.Root.Spawn(parentProps);
        var child = await system.Root.RequestAsync<PID>(parent, new GetChild());

        child.SendSystemMessage(system, new UnknownSystemMessage());

        Assert.True(parentStats.Reset.Wait(TimeSpan.FromSeconds(5)));
        var failure = Assert.Single(parentStats.Received.OfType<Failure>());
        Assert.Equal(child, failure.Who);
        Assert.IsType<InvalidOperationException>(failure.Reason);
    }
}
