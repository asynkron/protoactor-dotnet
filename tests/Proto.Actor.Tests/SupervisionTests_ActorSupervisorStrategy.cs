using System;
using System.Threading.Tasks;
using Proto;
using Xunit;

namespace Proto.Tests;

public class SupervisionTestsActorSupervisorStrategy
{
    [Fact]
    public async Task ActorImplementingISupervisorStrategy_Should_HandleFailureWithCustomStrategy()
    {
        await using var system = new ActorSystem();
        var customHandled = new TaskCompletionSource<bool>();
        var defaultHandled = new TaskCompletionSource<bool>();

        var childProps = Props.FromProducer(() => new FailingChildActor());
        var defaultStrategy = new TrackingSupervisorStrategy(defaultHandled);

        var parentProps = Props.FromProducer(() => new SupervisingActor(childProps, customHandled))
            .WithChildSupervisorStrategy(defaultStrategy);

        var parent = system.Root.Spawn(parentProps);
        system.Root.Send(parent, "fail");

        await customHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.False(defaultHandled.Task.IsCompleted);
    }

    [Fact]
    public async Task ActorWithoutISupervisorStrategy_Should_UseDefaultStrategy()
    {
        await using var system = new ActorSystem();
        var defaultHandled = new TaskCompletionSource<bool>();

        var childProps = Props.FromProducer(() => new FailingChildActor());
        var defaultStrategy = new TrackingSupervisorStrategy(defaultHandled);

        var parentProps = Props.FromProducer(() => new NonSupervisingParent(childProps))
            .WithChildSupervisorStrategy(defaultStrategy);

        var parent = system.Root.Spawn(parentProps);
        system.Root.Send(parent, "fail");

        await defaultHandled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    private class SupervisingActor : IActor, ISupervisorStrategy
    {
        private readonly Props _childProps;
        private readonly TaskCompletionSource<bool> _handled;
        private PID? _child;

        public SupervisingActor(Props childProps, TaskCompletionSource<bool> handled)
        {
            _childProps = childProps;
            _handled = handled;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _child = context.Spawn(_childProps);
                    break;
                case string:
                    context.Send(_child!, "fail");
                    break;
            }

            return Task.CompletedTask;
        }

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message)
        {
            _handled.TrySetResult(true);
            supervisor.ResumeChildren(child);
        }
    }

    private class NonSupervisingParent : IActor
    {
        private readonly Props _childProps;
        private PID? _child;

        public NonSupervisingParent(Props childProps) => _childProps = childProps;

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started:
                    _child = context.Spawn(_childProps);
                    break;
                case string:
                    context.Send(_child!, "fail");
                    break;
            }

            return Task.CompletedTask;
        }
    }

    private class FailingChildActor : IActor
    {
        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string)
            {
                throw new Exception("boom");
            }

            return Task.CompletedTask;
        }
    }

    private class TrackingSupervisorStrategy : ISupervisorStrategy
    {
        private readonly TaskCompletionSource<bool> _handled;

        public TrackingSupervisorStrategy(TaskCompletionSource<bool> handled) => _handled = handled;

        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception cause, object? message)
        {
            _handled.TrySetResult(true);
            supervisor.ResumeChildren(child);
        }
    }
}
