using System;
using System.Threading.Tasks;
using Xunit;

namespace Proto.Tests;

public class SupervisionTestsAlwaysRestart
{
    private static readonly Exception Exception = new("boo hoo");

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

    private class ParentActor : IActor
    {
        private readonly Props _child1Props;
        private readonly Props _child2Props;

        public ParentActor(Props child1Props, Props child2Props)
        {
            _child1Props = child1Props;
            _child2Props = child2Props;
        }

        private PID? Child1 { get; set; }
        private PID? Child2 { get; set; }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                Child1 = context.Spawn(_child1Props);
                Child2 = context.Spawn(_child2Props);
            }

            if (context.Message is string)
            {
                context.Forward(Child1!);
            }

            return Task.CompletedTask;
        }
    }

    private class ChildActor : IActor
    {
        private readonly Action _onStarted;

        public ChildActor(Action onStarted)
        {
            _onStarted = onStarted;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _onStarted();
                    break;
                case string:
                    throw Exception;
            }

            return Task.CompletedTask;
        }
    }
}

