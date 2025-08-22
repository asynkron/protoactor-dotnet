using Proto;

namespace Proto.TestFixtures;

/// <summary>
/// Shared helpers for spawning actors during tests.
/// </summary>
public static class Spawners
{
    /// <summary>
    /// Spawns an actor using the provided receive function.
    /// </summary>
    /// <param name="context">Root context used to spawn the actor.</param>
    /// <param name="receive">Receive function executed by the actor.</param>
    /// <returns>The <see cref="PID"/> of the spawned actor.</returns>
    public static PID SpawnActorFromFunc(this IRootContext context, Receive receive) =>
        context.Spawn(Props.FromFunc(receive));

    /// <summary>
    /// Spawns a forwarder actor using the provided receive function.
    /// </summary>
    /// <param name="context">Root context used to spawn the actor.</param>
    /// <param name="forwarder">Receive function executed by the forwarder.</param>
    /// <returns>The <see cref="PID"/> of the spawned forwarder actor.</returns>
    public static PID SpawnForwarderFromFunc(this IRootContext context, Receive forwarder) =>
        context.Spawn(Props.FromFunc(forwarder));
}
