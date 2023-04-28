using System.Threading.Tasks;
using Proto.Extensions;

namespace Proto.Remote;

public interface IRemote : IActorSystemExtension<IRemote>
{
    /// <summary>
    ///     Remote extension configuration
    /// </summary>
    RemoteConfigBase Config { get; }

    /// <summary>
    ///     Actor system that this extension is attached to
    /// </summary>
    ActorSystem System { get; }

    /// <summary>
    ///     True if the Remote extension has started
    /// </summary>
    bool Started { get; }

    /// <summary>
    ///     <see cref="BlockList" /> contains all members that have been blocked from communication, e.g. due to
    ///     unresponsiveness.
    /// </summary>
    BlockList BlockList { get; }

    /// <summary>
    ///     Shuts down the Remote extension
    /// </summary>
    /// <param name="graceful"></param>
    /// <returns></returns>
    Task ShutdownAsync(bool graceful = true);

    /// <summary>
    ///     Starts the Remote extension
    /// </summary>
    /// <returns></returns>
    Task StartAsync();
}