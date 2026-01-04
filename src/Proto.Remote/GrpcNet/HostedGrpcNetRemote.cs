using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcNet;

public class HostedGrpcNetRemote : BaseGrpcNetRemote
{
    private readonly EndpointManager _endpointManager;
    private readonly ILogger _logger;

    public HostedGrpcNetRemote(
        ActorSystem system,
        RemoteConfig config,
        EndpointManager endpointManager,
        ILogger<HostedGrpcNetRemote> logger
    ) : base(system, config)
    {
        _endpointManager = endpointManager;
        _logger = logger;
    }

    protected override ILogger Logger => _logger;
    protected override EndpointManager EndpointManager => _endpointManager;

    public IServerAddressesFeature? ServerAddressesFeature { get; set; }

    public override Task StartAsync()
    {
        lock (_lock)
        {
            if (Started)
            {
                return Task.CompletedTask;
            }

            var uri = _config.UriChooser(ServerAddressesFeature?.Addresses.Select(address => new Uri(address)));
            var boundPort = uri?.Port ?? Config.Port;
            var host = uri?.Host ?? Config.Host;

            System.SetAddress(Config.AdvertisedHost ?? host,
                Config.AdvertisedPort ?? boundPort
            );

            _endpointManager.Start();

            _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", host, boundPort,
                System.Address);

            CompleteStartup("Started HostedGrpcNet Successfully");
            return Task.CompletedTask;
        }
    }

    public override async Task ShutdownAsync(bool graceful = true)
    {
        lock (_lock)
        {
            if (!Started)
            {
                return;
            }

            Started = false;
        }

        try
        {
            await _endpointManager.StopAsync().ConfigureAwait(false);

            _logger.LogInformation(
                "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                System.Address, graceful
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Proto.Actor server stopped on {Address} with error: {MessagePayload}",
                System.Address, ex.Message
            );

            throw;
        }
    }
}
