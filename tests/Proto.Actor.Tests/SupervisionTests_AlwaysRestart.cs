using System;
using System.Linq;
using System.Threading.Tasks;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class SupervisionTestsAlwaysRestart
{
    private static readonly Exception Exception = new("boom");

    [Fact]
    public async Task AlwaysRestartStrategy_Should_RestartFailingChildOnly()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        var child1Started = 0;
        var child2Started = 0;
        var strategy = new AlwaysRestartStrategy();

        var child1Props = Props.FromProducer(() => new ChildActor(() => child1Started++));
        var child2Props = Props.FromProducer(() => new ChildActor(() => child2Started++));

        var parentProps = Props.FromProducer(() => new ParentActor(child1Props, child2Props))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(parentProps);

        context.Send(parent, "fail");

        await Task.Delay(1000);

        Assert.Equal(2, child1Started);
        Assert.Equal(1, child2Started);
    }

    [Fact]
    public async Task AlwaysRestartStrategy_Should_RestartChildOnEveryFailure()
    {
        await using var system = new ActorSystem();
        var context = system.Root;

        // probe child mailbox to observe Restart system messages
        var probe = new TestProbe();
        var probePid = system.Root.Spawn(Props.FromProducer(() => probe));
        context.Send(probePid, "start");
        await probe.FishForMessageAsync<string>();

        var childProps = Props.FromProducer(() => new ChildActor())
            .WithMailboxProbe(probe);

        var parentProps = Props.FromProducer(() => new ParentActor(childProps))
            .WithChildSupervisorStrategy(Supervision.AlwaysRestartStrategy);

        var parent = context.Spawn(parentProps);

        context.Send(parent, "1");
        context.Send(parent, "2");
        context.Send(parent, "3");

        for (var i = 0; i < 3; i++)
        {
            var restart = await probe.FishForMessageAsync<Restart>();
            Assert.Equal(Exception, restart.Reason);
        }

        // Ensure no Stop message was processed
        await Assert.ThrowsAsync<TestKitException>(
            async () => await probe.FishForMessageAsync<Stop>(TimeSpan.FromMilliseconds(100)));
    }

    private class ParentActor : IActor
    {
        private readonly Props[] _childProps;
        private PID[]? _children;

        public ParentActor(params Props[] childProps)
        {
            _childProps = childProps;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _children = _childProps.Select(context.Spawn).ToArray();
                    break;
                case string when _children != null:
                    context.Forward(_children[0]);
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class ChildActor : IActor
    {
        private readonly Action? _onStarted;

        public ChildActor(Action? onStarted = null)
        {
            _onStarted = onStarted;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _onStarted?.Invoke();
                    break;
                case string:
                    throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}

