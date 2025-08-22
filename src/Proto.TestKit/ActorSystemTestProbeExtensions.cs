using Proto;

namespace Proto.TestKit;

/// <summary>
/// Convenience methods for creating <see cref="TestProbe"/> instances.
/// </summary>
public static class ActorSystemTestProbeExtensions
{
    /// <summary>
    /// Creates a <see cref="TestProbe"/> and spawns it on the given <see cref="ActorSystem"/>.
    /// </summary>
    public static (TestProbe probe, PID pid) CreateTestProbe(this ActorSystem system)
    {
        var probe = new TestProbe();
        var pid = system.Root.Spawn(Props.FromProducer(() => probe));
        return (probe, pid);
    }

    /// <summary>
    /// Creates a <see cref="TestProbe"/> and spawns it using the provided <see cref="RootContext"/>.
    /// </summary>
    public static (TestProbe probe, PID pid) CreateTestProbe(this RootContext root)
    {
        var probe = new TestProbe();
        var pid = root.Spawn(Props.FromProducer(() => probe));
        return (probe, pid);
    }
}
