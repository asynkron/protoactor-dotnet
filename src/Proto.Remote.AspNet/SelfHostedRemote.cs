// -----------------------------------------------------------------------
//   <copyright file="SelfHostedRemote.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Grpc.HealthCheck;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public class SelfHostedRemote : IRemote
    {
        private readonly ILogger _logger = Log.CreateLogger<Remote>();
        private IWebHost? _host;
        private readonly Remote _remote;
        private readonly ActorSystem _system;
        private readonly int _port;
        private readonly AspRemoteConfig _remoteConfig;
        private readonly EndpointReader _endpointReader;
        private readonly IPAddress _ipAddress;
        public bool IsStarted { get; private set; }
        public Serialization Serialization { get; }
        public RemoteKindRegistry RemoteKindRegistry { get; }
        public SelfHostedRemote(ActorSystem system, Action<RemoteConfiguration>? configure = null) : this(system, IPAddress.Any, 0, configure)
        {
        }
        public SelfHostedRemote(ActorSystem system, int port, Action<RemoteConfiguration>? configure = null) : this(system, IPAddress.Any, port, configure)
        {
        }
        public SelfHostedRemote(ActorSystem system, IPAddress ipAddress, int port = 0,
            Action<RemoteConfiguration>? configure = null)
        {
            system.Plugins.AddPlugin<IRemote>(this);
            _remoteConfig = new AspRemoteConfig();
            Serialization = new Serialization();
            RemoteKindRegistry = new RemoteKindRegistry();
            var remoteConfiguration = new RemoteConfiguration(Serialization, RemoteKindRegistry, _remoteConfig);
            configure?.Invoke(remoteConfiguration);
            var channelProvider = new ChannelProvider(_remoteConfig);
            var endpointManager = new EndpointManager(_remoteConfig, Serialization, system, channelProvider);
            _endpointReader = new EndpointReader(system, endpointManager, Serialization);
            _remote = new Remote(system, RemoteKindRegistry, endpointManager);
            _system = system;
            _ipAddress = ipAddress;
            _port = port;

            // Allows tu use Grpc.Net over http
            if (!_remoteConfig.UseHttps)
                AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            else if (_remoteConfig.ConfigureKestrel == null || _remoteConfig.ChannelOptions == null)
            {
                throw new Exception("Http not configured");
            }
        }

        public void Start()
        {
            if (IsStarted) return;
            IsStarted = true;
            IServerAddressesFeature? serverAddressesFeature = null;
            _host = new WebHostBuilder()
                .UseKestrel()
                .ConfigureKestrel(serverOptions =>
                    {
                        if (_remoteConfig.ConfigureKestrel == null)
                            serverOptions.Listen(IPAddress.Any, _port,
                                listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; }
                            );
                        else
                            serverOptions.Listen(_ipAddress, _port,
                                listenOptions => _remoteConfig.ConfigureKestrel(listenOptions)
                            );
                    }
                )
                .ConfigureServices((serviceCollection) =>
                    {
                        serviceCollection.AddGrpc();
                        serviceCollection.AddSingleton<Remoting.RemotingBase>(_endpointReader);
                        serviceCollection.AddSingleton<IRemote>(this);
                    }
                ).Configure(app =>
                    {
                        app.UseRouting();
                        app.UseEndpoints(endpoints =>
                        {
                            endpoints.MapGrpcService<Remoting.RemotingBase>();
                            endpoints.MapGrpcService<HealthServiceImpl>();
                        });

                        serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
                    }
                )
                .Start();
            var uri = serverAddressesFeature!.Addresses.Select(address => new Uri(address)).First();
            var address = "localhost";
            var boundPort = uri.Port;
            _system.ProcessRegistry.SetAddress(_remoteConfig.AdvertisedHostname ?? address,
                    _remoteConfig.AdvertisedPort ?? boundPort
                );
            _remote.Start();
            _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", address, boundPort,
                _system.ProcessRegistry.Address
            );
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            if (!IsStarted) return;
            else IsStarted = false;
            try
            {
                using (_host)
                {
                    if (graceful)
                    {
                        await _remote.ShutdownAsync(graceful);
                    }
                    _logger.LogDebug(
                        "Proto.Actor server stopped on {Address}. Graceful: {Graceful}",
                        _system.ProcessRegistry.Address, graceful
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex, "Proto.Actor server stopped on {Address} with error: {Message}",
                    _system.ProcessRegistry.Address, ex.Message
                );
                throw;
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