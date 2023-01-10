using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;

namespace Proto.Cluster;

public class ProtoActorLifecycleHost : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly bool _runAsClient;
    private readonly IHostApplicationLifetime _lifetime;
    private bool _shutdownViaActorSystem;

    public ProtoActorLifecycleHost(
        ActorSystem actorSystem,
        IHostApplicationLifetime lifetime,
        bool runAsClient
        )
    {
        _actorSystem = actorSystem;
        _runAsClient = runAsClient;
        _lifetime = lifetime;
    }

    public async Task StartAsync(CancellationToken _)
    {
        // Register a callback for when the actor system shuts down.
        _actorSystem.Shutdown.Register(() =>
        {
            if (_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                return;
            }
            _shutdownViaActorSystem = true;

            if (_actorSystem.Cluster().Config.ExitOnShutdown)
            {
                _lifetime.StopApplication();
            }
        });

        if (_runAsClient)
        {
            await _actorSystem.Cluster().StartClientAsync().ConfigureAwait(false);
        }
        else
        {
            await _actorSystem.Cluster().StartMemberAsync().ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_shutdownViaActorSystem)
        {
            await _actorSystem.Cluster().ShutdownCompleted.ConfigureAwait(false);
        }
        else
        {
            await _actorSystem.Cluster().ShutdownAsync(true, "Host process is stopping").ConfigureAwait(false);
        }
    }
}
