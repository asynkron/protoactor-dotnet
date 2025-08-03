using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.Mailbox;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests;

public class SupervisionTestsAlwaysRestart
{
    private static readonly Exception Exception = new("boom");

    [Fact]
    public async Task AlwaysRestartStrategy_Should_RestartChildOnEveryFailure()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var restartCount = 0;
        var childMailboxStats = new TestMailboxStatistics(msg =>
        {
            if (msg is Restart)
            {
                restartCount++;
                return restartCount == 3;
            }

            return false;
        });

        var childProps = Props.FromProducer(() => new ChildActor())
            .WithMailbox(() => UnboundedMailbox.Create(childMailboxStats));

        var parentProps = Props.FromProducer(() => new ParentActor(childProps))
            .WithChildSupervisorStrategy(Supervision.AlwaysRestartStrategy);

        var parent = context.Spawn(parentProps);

        context.Send(parent, "1");
        context.Send(parent, "2");
        context.Send(parent, "3");

        Assert.True(childMailboxStats.Reset.Wait(TimeSpan.FromSeconds(2)));
        Assert.Equal(3, childMailboxStats.Received.OfType<Restart>().Count());
        Assert.DoesNotContain(childMailboxStats.Posted, msg => msg is Stop);
    }

    private class ParentActor : IActor
    {
        private readonly Props _childProps;

        public ParentActor(Props childProps)
        {
            _childProps = childProps;
        }

        private PID? _child;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _child = context.Spawn(_childProps);
                    break;
                default:
                    if (_child != null)
                    {
                        context.Forward(_child);
                    }
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class ChildActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string)
            {
                throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}
