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

public class GrpcNetRemote : BaseGrpcNetRemote
{
    private static readonly ILogger _logger = Log.CreateLogger<GrpcNetRemote>();
    private EndpointManager _endpointManager = null!;
    private RemotingGrpcService _remotingGrpcService = null!;
    private HealthServiceImpl _healthCheck = null!;
    private IWebHost? _host;

    public GrpcNetRemote(ActorSystem system, RemoteConfig config) : base(system, config)
    {
    }

    protected override ILogger Logger => _logger;
    protected override EndpointManager EndpointManager => _endpointManager;

    public async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        var endpoints = await _endpointManager.GetDiagnostics().ConfigureAwait(false);

        return endpoints;
    }

    public override Task StartAsync()
    {
        lock (_lock)
        {
            if (Started)
            {
                return Task.CompletedTask;
            }

            _endpointManager = new EndpointManager(System, Config);
            _remotingGrpcService = new RemotingGrpcService(System, _endpointManager);
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

                        serviceCollection.AddSingleton<Remoting.RemotingBase>(_remotingGrpcService);
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

            CompleteStartup("Started GrpcNet Successfully");

            return Task.CompletedTask;
        }
    }

    protected override async Task OnShutdownAsync(bool graceful)
    {
        using (_host)
        {
            if (_host is not null && graceful)
            {
                await _host.StopAsync().WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
            }
        }
    }
}
