using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster;

public class ProtoActorLifecycleHost : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly ILogger<ProtoActorLifecycleHost> _logger;
    private readonly bool _runAsClient;
    private readonly IHostApplicationLifetime _lifetime;
    private bool _shutdownViaActorSystem;

    public ProtoActorLifecycleHost(
        ActorSystem actorSystem,
        ILogger<ProtoActorLifecycleHost> logger,
        IHostApplicationLifetime lifetime,
        bool runAsClient
        )
    {
        _actorSystem = actorSystem;
        _logger = logger;
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
                _logger.LogWarning("[ProtoActorLifecycleHost]{SystemId} Exit on shutdown is enabled, shutting down host process", _actorSystem.Id);
                _lifetime.StopApplication();
            }
        });

        if (_runAsClient)
        {
            _logger.LogInformation("[ProtoActorLifecycleHost]{SystemId} Starting Proto.Actor cluster client", _actorSystem.Id);
            await _actorSystem.Cluster().StartClientAsync().ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("[ProtoActorLifecycleHost]{SystemId} Starting Proto.Actor cluster member", _actorSystem.Id);
            await _actorSystem.Cluster().StartMemberAsync().ConfigureAwait(false);
        }
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_shutdownViaActorSystem)
        {
            _logger.LogInformation("[ProtoActorLifecycleHost]{SystemId} Stopping Proto.Actor cluster via actor system", _actorSystem.Id);
            await _actorSystem.Cluster().ShutdownCompleted.ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("[ProtoActorLifecycleHost]{SystemId} Stopping Proto.Actor cluster via host application lifetime (SIGTERM)", _actorSystem.Id);
            await _actorSystem.Cluster().ShutdownAsync(true, "Host process is stopping").ConfigureAwait(false);
        }
    }
}
