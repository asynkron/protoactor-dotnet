using JetBrains.Annotations;

namespace Proto.Remote;

[PublicAPI]
public static class ActorSystemExtensions
{
    /// <summary>
    ///     Gets the <see cref="Serialization" /> subsystem for the given <see cref="ActorSystem" />.
    /// </summary>
    /// <param name="system"></param>
    /// <returns></returns>
    public static Serialization Serialization(this ActorSystem system) =>
        system.Extensions.GetRequired<Serialization>();
}