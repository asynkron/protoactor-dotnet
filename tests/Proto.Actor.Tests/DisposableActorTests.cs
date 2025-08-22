using System;
using System.Threading.Tasks;
using Proto.TestKit;
using Xunit;

namespace Proto.Tests;

public class DisposableActorTests
{
    [Fact]
    public async Task WhenActorRestarted_DisposeIsCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 0, null);

        var childProps = Props.FromProducer(() => new DisposableActor(system, probePid, "disposed"))
            .WithChildSupervisorStrategy(strategy);

        var props = Props.FromProducer(() => new SupervisingActor(childProps))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(props);
        context.Send(parent, "crash");

        await probe.ExpectNextUserMessageAsync<string>(s => s == "disposed");
    }

    [Fact]
    public async Task WhenActorRestarted_DisposeAsyncIsCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Restart, 0, null);

        var childProps = Props.FromProducer(() => new AsyncDisposableActor(system, probePid, "disposed"))
            .WithChildSupervisorStrategy(strategy);

        var props = Props.FromProducer(() => new SupervisingActor(childProps))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(props);
        context.Send(parent, "crash");

        await probe.ExpectNextUserMessageAsync<string>(s => s == "disposed");
    }

    [Fact]
    public async Task WhenActorResumed_DisposeIsNotCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 0, null);

        var childProps = Props.FromProducer(() => new DisposableActor(system, probePid, "disposed"))
            .WithChildSupervisorStrategy(strategy);

        var props = Props.FromProducer(() => new SupervisingActor(childProps))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(props);
        context.Send(parent, "crash");

        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task WhenActorResumed_DisposeAsyncIsNotCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var strategy = new OneForOneStrategy((pid, reason) => SupervisorDirective.Resume, 0, null);

        var childProps = Props.FromProducer(() => new AsyncDisposableActor(system, probePid, "disposed"))
            .WithChildSupervisorStrategy(strategy);

        var props = Props.FromProducer(() => new SupervisingActor(childProps))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(props);
        context.Send(parent, "crash");

        await probe.ExpectEmptyMailboxAsync();
    }

    [Fact]
    public async Task WhenActorStopped_DisposeIsCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();

        var props = Props.FromProducer(() => new DisposableActor(system, probePid, "disposed"));

        var pid = context.Spawn(props);
        await context.StopAsync(pid);
        await probe.ExpectNextUserMessageAsync<string>(s => s == "disposed");
    }

    [Fact]
    public async Task WhenActorStopped_DisposeAsyncIsCalled()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();

        var props = Props.FromProducer(() => new AsyncDisposableActor(system, probePid, "disposed"));

        var pid = context.Spawn(props);
        await context.StopAsync(pid);
        await probe.ExpectNextUserMessageAsync<string>(s => s == "disposed");
    }

    [Fact]
    public async Task WhenActorWithChildrenStopped_DisposeIsCalledInEachChild()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var (probe, probePid) = system.CreateTestProbe();
        var strategy = new AllForOneStrategy((pid, reason) => SupervisorDirective.Stop, 1, null);

        var child1Props = Props.FromProducer(() => new DisposableActor(system, probePid, "child1"));

        var child2Props = Props.FromProducer(() => new DisposableActor(system, probePid, "child2"));

        var parentProps = Props.FromProducer(() => new ParentWithMultipleChildrenActor(child1Props, child2Props))
            .WithChildSupervisorStrategy(strategy);

        var parent = context.Spawn(parentProps);

        context.Send(parent, "crash");

        var messages = new[]
        {
            await probe.GetNextUserMessageAsync<string>(),
            await probe.GetNextUserMessageAsync<string>()
        };

        Assert.Contains("child1", messages);
        Assert.Contains("child2", messages);
    }

    private class SupervisingActor : IActor
    {
        private readonly Props _childProps;
        private PID? _childPid;

        public SupervisingActor(Props childProps)
        {
            _childProps = childProps;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is Started)
            {
                _childPid = context.Spawn(_childProps);
            }

            if (context.Message is string)
            {
                context.Send(_childPid!, context.Message);
            }

            return Task.CompletedTask;
        }
    }

    private class AsyncDisposableActor : IActor, IAsyncDisposable
    {
        private readonly ActorSystem _system;
        private readonly PID _probe;
        private readonly object _message;

        public AsyncDisposableActor(ActorSystem system, PID probe, object message)
        {
            _system = system;
            _probe = probe;
            _message = message;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string)
            {
                throw new Exception();
            }

            return Task.CompletedTask;
        }

        public ValueTask DisposeAsync()
        {
            _system.Root.Send(_probe, _message);

            return default;
        }
    }

    private class DisposableActor : IActor, IDisposable
    {
        private readonly ActorSystem _system;
        private readonly PID _probe;
        private readonly object _message;

        public DisposableActor(ActorSystem system, PID probe, object message)
        {
            _system = system;
            _probe = probe;
            _message = message;
        }

        public Task ReceiveAsync(IContext context)
        {
            if (context.Message is string)
            {
                throw new Exception();
            }

            return Task.CompletedTask;
        }

        public void Dispose() => _system.Root.Send(_probe, _message);
    }

    private class ParentWithMultipleChildrenActor : IActor
    {
        private readonly Props _child1Props;
        private readonly Props _child2Props;

        public ParentWithMultipleChildrenActor(Props child1Props, Props child2Props)
        {
            _child1Props = child1Props;
            _child2Props = child2Props;
        }

        private PID? Child1 { get; set; }
        private PID? Child2 { get; set; }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Child1 = context.Spawn(_child1Props);
                    Child2 = context.Spawn(_child2Props);

                    break;
                case string _:
                    context.Send(Child1!, context.Message);

                    break;
            }

            return Task.CompletedTask;
        }
    }
}