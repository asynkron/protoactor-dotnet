using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcNet
{
    public class HostedGrpcNetRemote : IRemote
    {
        private readonly ILogger _logger;
        private readonly EndpointManager _endpointManager;
        private readonly GrpcNetRemoteConfig _config;

        public IServerAddressesFeature? ServerAddressesFeature { get; set; }
        public RemoteConfigBase Config => _config;
        public ActorSystem System { get; }

        public HostedGrpcNetRemote(ActorSystem system, GrpcNetRemoteConfig config, EndpointManager endpointManager, ILogger<HostedGrpcNetRemote> logger)
        {
            System = system;
            _config = config;
            _endpointManager = endpointManager;
            _logger = logger;
            System.Extensions.Register(this);
        }
        public bool Started { get; private set; }
        public Task StartAsync()
        {
            lock (this)
            {
                if (Started)
                    return Task.CompletedTask;
                var uri = ServerAddressesFeature?.Addresses.Select(address => new Uri(address)).FirstOrDefault();
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
            lock (this)
            {
                if (!Started)
                    return Task.CompletedTask;
                try
                {
                    _endpointManager.Stop();
                    _logger.LogDebug(
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

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            _endpointManager.SendMessage(pid, msg, serializerId);
        }
    }
}