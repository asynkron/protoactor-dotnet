using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using Proto.Remote.Metrics;

namespace Proto.Remote.GrpcNet
{
    public class HostedGrpcNetRemote : IRemote
    {
        private readonly object _lock = new();
        private readonly GrpcNetRemoteConfig _config;
        private readonly EndpointManager _endpointManager;
        private readonly ILogger _logger;

        public HostedGrpcNetRemote(
            ActorSystem system,
            GrpcNetRemoteConfig config,
            EndpointManager endpointManager,
            ILogger<HostedGrpcNetRemote> logger
        )
        {
            System = system;
            _config = config;
            _endpointManager = endpointManager;
            _logger = logger;
            System.Extensions.Register(this);
            System.Extensions.Register(config.Serialization);
        }

        public IServerAddressesFeature? ServerAddressesFeature { get; set; }
        public RemoteConfigBase Config => _config;
        public ActorSystem System { get; }
        public bool Started { get; private set; }

        public BlockList BlockList { get; } = new();

        public Task StartAsync()
        {
            lock (_lock)
            {
                if (Started)
                    return Task.CompletedTask;

                var uri = _config.UriChooser(ServerAddressesFeature?.Addresses.Select(address => new Uri(address)));
                var boundPort = uri?.Port ?? Config.Port;
                var host = uri?.Host ?? Config.Host;
                System.SetAddress(Config.AdvertisedHost ?? host,
                    Config.AdvertisedPort ?? boundPort
                );
                _endpointManager.Start();
                _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", host, boundPort, System.Address);
                Started = true;
                return Task.CompletedTask;
            }
        }

        public Task ShutdownAsync(bool graceful = true)
        {
            lock (_lock)
            {
                if (!Started)
                    return Task.CompletedTask;

                try
                {
                    _endpointManager.Stop();
                    _logger.LogInformation(
                        "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                        System.Address, graceful
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                        System.Address, ex.Message
                    );
                    throw;
                }

                Started = false;
                return Task.CompletedTask;
            }
        }
    }
}