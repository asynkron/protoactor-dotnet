using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote.Metrics;

namespace Proto.Remote.GrpcNet
{
    public class GrpcNetClientRemote : IRemote
    {
        private readonly object _lock = new();
        private readonly GrpcNetRemoteConfig _config;
        public RemoteConfigBase Config => _config;

        public ActorSystem System { get; }
        public BlockList BlockList { get; } = new();
        public bool Started { get; private set; }
        private readonly ILogger _logger = Log.CreateLogger<GrpcNetClientRemote>();
        private readonly EndpointManager _endpointManager;

        public GrpcNetClientRemote(ActorSystem system, GrpcNetRemoteConfig config)
        {
            System = system;
            if (config.AdvertisedHost is not null)
                throw new ArgumentException("AdvertisedHost is not supported in client mode");
            if (config.Port != 0)
                throw new ArgumentException("Choosing port is not supported in client mode");
            System.SetClientAddress();
            _config = config;
            System.Extensions.Register(this);
            System.Extensions.Register(config.Serialization);
            var channelProvider = new GrpcNetChannelProvider(_config);
            _endpointManager = new EndpointManager(System, Config, channelProvider);
        }
        public Task ShutdownAsync(bool graceful = true)
        {
            lock (_lock)
            {
                if (!Started)
                    return Task.CompletedTask;

                Started = false;
            }

            try
            {
                if (graceful)
                {
                    _endpointManager.Stop();
                }

                _logger.LogInformation(
                    "Proto.Actor client {Id} stopped. Graceful: {Graceful}",
                    System.Id, graceful
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Proto.Actor client {Id} stopped with error: {Message}",
                    System.Id, ex.Message
                );
            }
            return Task.CompletedTask;
        }
        public Task StartAsync()
        {
            _endpointManager.Start();
            _logger.LogInformation("Starting Proto.Actor client ({Address})", System.Id);
            Started = true;
            return Task.CompletedTask;
        }
    }
}