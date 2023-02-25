using System;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Grpc.Health.V1;
using Grpc.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;

namespace Proto.Remote.GrpcNet;

public class GrpcNetRemote : IRemote
{
    private readonly GrpcNetRemoteConfig _config;
    private readonly object _lock = new();
    private readonly ILogger _logger = Log.CreateLogger<GrpcNetRemote>();
    private EndpointManager _endpointManager = null!;
    private EndpointReader _endpointReader = null!;
    private HealthServiceImpl _healthCheck = null!;
    private IWebHost? _host;

    public GrpcNetRemote(ActorSystem system, GrpcNetRemoteConfig config)
    {
        System = system;
        BlockList = new BlockList(system);
        _config = config;
        System.Extensions.Register(this);
        System.Extensions.Register(config.Serialization);
    }

    public bool Started { get; private set; }

    public BlockList BlockList { get; }

    public RemoteConfigBase Config => _config;
    public ActorSystem System { get; }

    public async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        var endpoints = await _endpointManager.GetDiagnostics().ConfigureAwait(false);

        return endpoints;
    }

    public Task StartAsync()
    {
        lock (_lock)
        {
            if (Started)
            {
                return Task.CompletedTask;
            }

            var channelProvider = new GrpcNetChannelProvider(_config);
            _endpointManager = new EndpointManager(System, Config, channelProvider);
            _endpointReader = new EndpointReader(System, _endpointManager);
            _healthCheck = new HealthServiceImpl();

            if (!IPAddress.TryParse(Config.Host, out var ipAddress))
            {
                ipAddress = IPAddress.Any;
            }

            IServerAddressesFeature? serverAddressesFeature = null;

            _host = new WebHostBuilder()
                .UseKestrel()
                .ConfigureKestrel(serverOptions =>
                    {
                        if (_config.ConfigureKestrel == null)
                        {
                            serverOptions.Listen(ipAddress, Config.Port,
                                listenOptions => { listenOptions.Protocols = HttpProtocols.Http2; }
                            );
                        }
                        else
                        {
                            serverOptions.Listen(ipAddress, Config.Port,
                                listenOptions => _config.ConfigureKestrel(listenOptions)
                            );
                        }
                    }
                )
                .ConfigureServices(serviceCollection =>
                    {
                        serviceCollection.AddSingleton(Log.GetLoggerFactory());

                        serviceCollection.AddGrpc(options =>
                            {
                                options.MaxReceiveMessageSize = null;
                                options.EnableDetailedErrors = true;
                            }
                        );

                        serviceCollection.AddSingleton<Remoting.RemotingBase>(_endpointReader);
                        serviceCollection.AddSingleton<Health.HealthBase>(_healthCheck);
                        serviceCollection.AddSingleton<IRemote>(this);
                    }
                )
                .Configure(app =>
                    {
                        app.UseRouting();

                        app.UseEndpoints(endpoints =>
                            {
                                endpoints.MapGrpcService<Remoting.RemotingBase>();
                                endpoints.MapGrpcService<Health.HealthBase>();
                            }
                        );

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

            _logger.LogInformation("Starting Proto.Actor server on {Host}:{Port} ({Address})", Config.Host, Config.Port,
                System.Address);

            Started = true;
            System.Diagnostics.RegisterEvent("Remote", "Started GrpcNet Successfully");
            System.Diagnostics.RegisterObject("Remote", "Config", Config);
            Config.Serialization.Init(System);

            return Task.CompletedTask;
        }
    }

    public async Task ShutdownAsync(bool graceful = true)
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
            using (_host)
            {
                if (graceful)
                {
                    _endpointManager.Stop();

                    if (_host is not null)
                    {
                        await _host.StopAsync().ConfigureAwait(false);
                    }
                }
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
    }
}