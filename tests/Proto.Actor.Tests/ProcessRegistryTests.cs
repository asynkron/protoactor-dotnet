using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Tests;

public class ProcessRegistryTests
{
    [Fact]
    public async Task Given_PIDDoesNotExist_TryAddShouldAddLocalPID()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var id = Guid.NewGuid().ToString();
        var p = new TestProcess(system);
        var reg = new ProcessRegistry(system);

        var (pid, ok) = reg.TryAdd(id, p);

        Assert.True(ok);
        Assert.Equal(system.Address, pid.Address);
        Assert.Equal(id, pid.Id);
    }

    [Fact]
    public async Task Given_PIDExists_TryAddShouldNotAddLocalPID()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var id = Guid.NewGuid().ToString();
        var p = new TestProcess(system);
        var reg = new ProcessRegistry(system);
        var (pid1, _) = reg.TryAdd(id, p);

        var (pid2, ok) = reg.TryAdd(id, p);

        Assert.False(ok);
        Assert.Equal(pid1.Id, pid2.Id);
        Assert.Equal(pid1.Address, pid2.Address);
    }

    [Fact]
    public async Task Given_PIDExists_GetShouldReturnIt()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var id = Guid.NewGuid().ToString();
        var p = new TestProcess(system);
        var reg = new ProcessRegistry(system);
        reg.TryAdd(id, p);
        var (pid, _) = reg.TryAdd(id, p);

        var p2 = reg.Get(pid);

        Assert.Same(p, p2);
        Assert.Equal(id, pid.Id);
    }

    [Fact]
    public async Task Given_PIDWasRemoved_GetShouldReturnDeadLetterProcess()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var id = Guid.NewGuid().ToString();
        var p = new TestProcess(system);
        var reg = new ProcessRegistry(system);
        var (pid, _) = reg.TryAdd(id, p);
        reg.Remove(pid);

        var p2 = reg.Get(pid);

        Assert.Same(system.DeadLetter, p2);
    }

    [Fact]
    public async Task Given_PIDExistsInHostResolver_GetShouldReturnIt()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var pid = new PID();
        var p = new TestProcess(system);
        var reg = new ProcessRegistry(system);
        reg.RegisterHostResolver(x => p);

        var p2 = reg.Get(pid);

        Assert.Same(p, p2);
    }
}
