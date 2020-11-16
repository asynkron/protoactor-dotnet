// -----------------------------------------------------------------------
//   <copyright file="GrpcCoreRemote.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcCore
{
    [PublicAPI]
    public class GrpcCoreRemote : IRemote
    {
        private static readonly ILogger Logger = Log.CreateLogger<GrpcCoreRemote>();
        private EndpointManager _endpointManager = null!;
        private EndpointReader _endpointReader = null!;
        private HealthServiceImpl _healthCheck = null!;
        private Server _server = null!;
        private readonly GrpcCoreRemoteConfig _config;

        public GrpcCoreRemote(ActorSystem system, GrpcCoreRemoteConfig config)
        {
            System = system;
            _config = config;
            System.Extensions.Register(this);
            System.Extensions.Register(config.Serialization);
        }
        public bool Started { get; private set; }
        public ActorSystem System { get; }
        public RemoteConfigBase Config => _config;

        public Task StartAsync()
        {
            lock (this)
            {
                if (Started)
                    return Task.CompletedTask;
                var channelProvider = new GrpcCoreChannelProvider(_config);
                _endpointManager = new EndpointManager(System, Config, channelProvider);
                _endpointReader = new EndpointReader(System, _endpointManager, Config.Serialization);
                _healthCheck = new HealthServiceImpl();
                _server = new Server
                {
                    Services =
                    {
                        Remoting.BindService(_endpointReader),
                        Health.BindService(_healthCheck)
                    },
                    Ports = { new ServerPort(Config.Host, Config.Port, _config.ServerCredentials) }
                };
                _server.Start();

                var boundPort = _server.Ports.Single().BoundPort;
                System.SetAddress(Config.AdvertisedHost ?? Config.Host, Config.AdvertisedPort ?? boundPort
                );
                _endpointManager.Start();

                Logger.LogDebug("Starting Proto.Actor server on {Host}:{Port} ({Address})", Config.Host, boundPort,
                    System.Address
                );
                Started = true;
                return Task.CompletedTask;
            }
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            lock (this)
            {
                if (!Started)
                    return;
                Started = false;
            }
            try
            {
                if (graceful)
                {
                    _endpointManager.Stop();
                    await _server.ShutdownAsync();
                }
                else
                {
                    await _server.KillAsync();
                }

                Logger.LogDebug(
                    "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                    System.Address, graceful
                );
            }
            catch (Exception ex)
            {
                Logger.LogError(
                    ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                    System.Address, ex.Message
                );
                await _server.KillAsync();
            }
        }

        public void SendMessage(PID pid, object msg, int serializerId)
        {
            _endpointManager.SendMessage(pid, msg, serializerId);
        }
    }
}