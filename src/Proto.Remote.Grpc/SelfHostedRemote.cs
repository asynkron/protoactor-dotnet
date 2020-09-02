// -----------------------------------------------------------------------
//   <copyright file="SelfHostedRemote.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class SelfHostedRemote : IRemote
    {
        private readonly ILogger _logger = Log.CreateLogger<Remote>();
        private readonly Server _server = null!;
        private readonly Remote _remote;
        private readonly ActorSystem _system;
        private readonly string _hostname;
        private readonly GrpcRemoteConfig _remoteConfig;
        public bool IsStarted { get; private set; }
        public Serialization Serialization { get; }
        public RemoteKindRegistry RemoteKindRegistry { get; }

        public SelfHostedRemote(ActorSystem system, string hostname = "localhost", int port = 0,
            Action<RemoteConfiguration>? configure = null)
        {
            system.Plugins.AddPlugin<IRemote>(this);
            _remoteConfig = new GrpcRemoteConfig();
            Serialization = new Serialization();
            RemoteKindRegistry = new RemoteKindRegistry();
            var remoteConfiguration = new RemoteConfiguration(Serialization, RemoteKindRegistry, _remoteConfig);
            configure?.Invoke(remoteConfiguration);
            var channelProvider = new ChannelProvider(_remoteConfig);
            var endpointManager = new EndpointManager(_remoteConfig, Serialization, system, channelProvider);
            var endpointReader = new EndpointReader(system, endpointManager, Serialization);
            var healthCheck = new HealthServiceImpl();
            _server = new Server
            {
                Services =
                {
                    Remoting.BindService(endpointReader),
                    Health.BindService(healthCheck)
                },
                Ports = { new ServerPort(hostname, port, _remoteConfig.ServerCredentials) }
            };
            _remote = new Remote(system, RemoteKindRegistry, endpointManager);
            _system = system;
            _hostname = hostname;
        }

        public void Start()
        {
            if (IsStarted) return;
            _server.Start();
            var boundPort = _server.Ports.Single().BoundPort;
            _system.ProcessRegistry.SetAddress(_remoteConfig.AdvertisedHostname ?? _hostname,
                _remoteConfig.AdvertisedPort ?? boundPort
            );
            _remote.Start();
            _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", _hostname, boundPort,
                _system.ProcessRegistry.Address
            );
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            if (!IsStarted) return;
            else IsStarted = false;
            try
            {
                await _remote.ShutdownAsync(graceful);
                if (graceful)
                {
                    await _server.KillAsync(); //TODO: was ShutdownAsync but that never returns?
                }
                else
                {
                    await _server.KillAsync();
                }

                _logger.LogDebug(
                    "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                    _system.ProcessRegistry.Address, graceful
                );
            }
            catch (Exception ex)
            {
                await _server.KillAsync();

                _logger.LogError(
                    ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                    _system.ProcessRegistry.Address, ex.Message
                );
            }
        }

        public Task<ActorPidResponse> SpawnAsync(string address, string kind, TimeSpan timeout)
        {
            return _remote.SpawnAsync(address, kind, timeout);
        }

        public Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            return _remote.SpawnNamedAsync(address, name, kind, timeout);
        }

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            _remote.SendMessage(pid, msg, serializerId);
        }
    }
}