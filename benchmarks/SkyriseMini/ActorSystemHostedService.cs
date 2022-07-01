using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace ProtoActorSut.Shared;

public class ActorSystemHostedService : IHostedService
{
    private readonly ActorSystem _actorSystem;
    private readonly ILogger<ActorSystemHostedService> _logger;

    public ActorSystemHostedService(ActorSystem actorSystem, ILogger<ActorSystemHostedService> logger)
    {
        _actorSystem = actorSystem;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting Proto actor system");
            
        await _actorSystem
            .Cluster()
            .StartMemberAsync();
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping Proto actor system");

        await _actorSystem
            .Cluster()
            .ShutdownAsync();
    }
}