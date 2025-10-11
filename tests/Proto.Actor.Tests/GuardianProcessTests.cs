using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Proto.Mailbox;
using Xunit;

namespace Proto.Tests;

public class GuardianProcessTests
{
    private class NoopStrategy : ISupervisorStrategy
    {
        public void HandleFailure(ISupervisor supervisor, PID child, RestartStatistics rs, Exception reason, object? message)
        {
        }
    }

    private class RecordingProcess : Process
    {
        public List<SystemMessage> Messages { get; } = new();

        public RecordingProcess(ActorSystem system) : base(system)
        {
        }

        protected internal override void SendUserMessage(PID pid, object message)
        {
        }

        protected internal override void SendSystemMessage(PID pid, SystemMessage message)
        {
            Messages.Add(message);
        }
    }

    [Fact]
    public async Task Given_Guardian_SendUserMessageShouldThrow()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var guardian = new GuardianProcess(system, new NoopStrategy());

        Assert.Throws<InvalidOperationException>(() => guardian.SendUserMessage(guardian.Pid, "hello"));
    }

    [Fact]
    public async Task Given_Guardian_RestartChildrenShouldSendRestartMessage()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var guardian = new GuardianProcess(system, new NoopStrategy());
        var child1 = new RecordingProcess(system);
        var child2 = new RecordingProcess(system);
        var (pid1, _) = system.ProcessRegistry.TryAdd(Guid.NewGuid().ToString(), child1);
        var (pid2, _) = system.ProcessRegistry.TryAdd(Guid.NewGuid().ToString(), child2);
        var reason = new Exception("boom");

        guardian.RestartChildren(reason, pid1, pid2);

        var msg1 = Assert.IsType<Restart>(Assert.Single(child1.Messages));
        var msg2 = Assert.IsType<Restart>(Assert.Single(child2.Messages));
        Assert.Same(reason, msg1.Reason);
        Assert.Same(reason, msg2.Reason);
    }

    [Fact]
    public async Task Given_Guardian_StopChildrenShouldSendStopMessage()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var guardian = new GuardianProcess(system, new NoopStrategy());
        var child1 = new RecordingProcess(system);
        var child2 = new RecordingProcess(system);
        var (pid1, _) = system.ProcessRegistry.TryAdd(Guid.NewGuid().ToString(), child1);
        var (pid2, _) = system.ProcessRegistry.TryAdd(Guid.NewGuid().ToString(), child2);

        guardian.StopChildren(pid1, pid2);

        Assert.IsType<Stop>(Assert.Single(child1.Messages));
        Assert.IsType<Stop>(Assert.Single(child2.Messages));
    }

    [Fact]
    public async Task Given_Guardian_EscalateFailureShouldThrow()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var guardian = new GuardianProcess(system, new NoopStrategy());

        Assert.Throws<InvalidOperationException>(() => guardian.EscalateFailure(new Exception(), null));
    }
}

