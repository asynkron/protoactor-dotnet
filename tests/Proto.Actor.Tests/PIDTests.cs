using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;
using static Proto.TestFixtures.Receivers;

namespace Proto.Tests;

public class PidTests
{
    [Fact]
    public async Task Given_ActorNotDead_Ref_ShouldReturnIt()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;
        var pid = context.Spawn(Props.FromFunc(EmptyReceive));

        var p = pid.Ref(system);

        Assert.NotNull(p);
    }

    [Fact]
    public async Task Given_ActorDied_Ref_ShouldNotReturnIt()
    {
        var system = new ActorSystem();
        await using var _ = system;
        var context = system.Root;

        var pid = context.Spawn(Props.FromFunc(EmptyReceive).WithMailbox(() => new TestMailbox()));
        await context.StopAsync(pid);

        var p = pid.Ref(system);

        Assert.Null(p);
    }

    [Fact]
    public async Task Given_OtherProcess_Ref_ShouldReturnIt()
    {
        var system = new ActorSystem();
        await using var _ = system;

        var id = Guid.NewGuid().ToString();
        var p = new TestProcess(system);
        var (pid, _) = system.ProcessRegistry.TryAdd(id, p);

        var p2 = pid.Ref(system);

        Assert.Same(p, p2);
    }
}