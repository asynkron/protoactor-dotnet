using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcNet;

public class GrpcNetClientRemote : BaseGrpcNetRemote
{
    private static readonly ILogger _logger = Log.CreateLogger<GrpcNetClientRemote>();
    private readonly EndpointManager _endpointManager;

    public GrpcNetClientRemote(ActorSystem system, RemoteConfig config) : base(system, config)
    {
        if (config.AdvertisedHost is not null)
        {
            throw new ArgumentException("AdvertisedHost is not supported in client mode");
        }

        if (config.Port != 0)
        {
            throw new ArgumentException("Choosing port is not supported in client mode");
        }

        System.SetClientAddress();
        System.Diagnostics.RegisterObject("Remote", "Config", Config);
        config.Serialization.Init(system);
        _endpointManager = new EndpointManager(System, Config);
    }

    protected override ILogger Logger => _logger;
    protected override EndpointManager EndpointManager => _endpointManager;

    public override Task StartAsync()
    {
        _endpointManager.Start();
        _logger.LogInformation("Starting Proto.Actor client ({Address})", System.Id);
        Started = true;
        System.Diagnostics.RegisterEvent("Remote", "Started GrpcNetClient Successfully");
        System.Diagnostics.RegisterObject("Cluster", "Config", Config);
        return Task.CompletedTask;
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
            if (graceful)
            {
                await _endpointManager.StopAsync().ConfigureAwait(false);
            }

            _logger.LogInformation(
                "Proto.Actor client {Id} stopped. Graceful: {Graceful}",
                System.Id, graceful
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex, "Proto.Actor client {Id} stopped with error: {MessagePayload}",
                System.Id, ex.Message
            );
        }
    }
}
