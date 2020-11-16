using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Grpc.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Proto.Remote.GrpcNet
{
    public class GrpcNetRemote : IRemote
    {
        private readonly ILogger _logger = Log.CreateLogger<GrpcNetRemote>();
        private EndpointManager _endpointManager = null!;
        private EndpointReader _endpointReader = null!;
        private HealthServiceImpl _healthCheck = null!;
        private readonly GrpcNetRemoteConfig _config;
        private IWebHost? _host;
        public bool Started { get; private set; }
        public RemoteConfigBase Config => _config;
        public ActorSystem System { get; }
        public GrpcNetRemote(ActorSystem system, GrpcNetRemoteConfig config)
        {
            System = system;
            _config = config;
            System.Extensions.Register(this);
            System.Extensions.Register(config.Serialization);
        }
        public Task StartAsync()
        {
            lock (this)
            {
                if (Started)
                    return Task.CompletedTask;
                var channelProvider = new GrpcNetChannelProvider(_config);
                _endpointManager = new EndpointManager(System, Config, channelProvider);
                _endpointReader = new EndpointReader(System, _endpointManager, Config.Serialization);
                _healthCheck = new HealthServiceImpl();

                if (!IPAddress.TryParse(Config.Host, out var ipAddress))
                    ipAddress = IPAddress.Any;
                IServerAddressesFeature? serverAddressesFeature = null;

                _host = new WebHostBuilder()
                    .UseKestrel()
                    .ConfigureKestrel(serverOptions =>
                        {
                            if (_config.ConfigureKestrel == null)
                                serverOptions.Listen(ipAddress, Config.Port,
                                    listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; }
                                );
                            else
                                serverOptions.Listen(ipAddress, Config.Port,
                                    listenOptions => _config.ConfigureKestrel(listenOptions)
                                );
                        }
                    )
                    .ConfigureServices((serviceCollection) =>
                        {
                            serviceCollection.AddSingleton<ILoggerFactory>(Log.GetLoggerFactory());
                            serviceCollection.AddGrpc(options =>
                            {
                                options.MaxReceiveMessageSize = null;
                                options.EnableDetailedErrors = true;
                            });
                            serviceCollection.AddSingleton<Remoting.RemotingBase>(_endpointReader);
                            serviceCollection.AddSingleton<Grpc.Health.V1.Health.HealthBase>(_healthCheck);
                            serviceCollection.AddSingleton<IRemote>(this);
                        }
                    ).Configure(app =>
                        {
                            app.UseRouting();
                            app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<Remoting.RemotingBase>();
                                endpoints.MapGrpcService<Grpc.Health.V1.Health.HealthBase>();
                            });

                            serverAddressesFeature = app.ServerFeatures.Get<IServerAddressesFeature>();
                        }
                    )
                    .Start();
                var uri = serverAddressesFeature!.Addresses.Select(address => new Uri(address)).First();
                var boundPort = uri.Port;
                System.SetAddress(Config.AdvertisedHost ?? Config.Host,
                        Config.AdvertisedPort ?? boundPort
                    );
                _endpointManager.Start();
                _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", Config.Host, Config.Port, System.Address);
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
                    using (_host)
                    {
                        if (graceful)
                            _endpointManager.Stop();
                    }
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
                }
                Started = false;
                return Task.CompletedTask;
            }
        }

        // Only used in tests ?
        public void SendMessage(PID pid, object msg, int serializerId)
        {
            _endpointManager.SendMessage(pid, msg, serializerId);
        }
    }
}