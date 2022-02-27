#if NETCOREAPP3_1_OR_GREATER
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.Logging;
using Grpc.HealthCheck;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

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




namespace Proto.Remote.GrpcNet
{
    [PublicAPI]
    public static class Extensions2
    {
        private static void AddAllServices(IServiceCollection services)
        {
            services.TryAddSingleton<ActorSystem>();
            services.AddHostedService<RemoteHostedService>();
            services.AddSingleton<HostedGrpcNetRemote>();
            services.AddSingleton<IRemote, HostedGrpcNetRemote>(sp => sp.GetRequiredService<HostedGrpcNetRemote>());
            services.AddSingleton<EndpointManager>();
            services.AddSingleton<RemoteConfigBase, GrpcNetRemoteConfig>(sp => sp.GetRequiredService<GrpcNetRemoteConfig>());
            services.AddSingleton<EndpointReader, EndpointReader>();
            services.AddSingleton(sp => sp.GetRequiredService<GrpcNetRemoteConfig>().Serialization);
            services.AddSingleton<Remoting.RemotingBase, EndpointReader>(sp => sp.GetRequiredService<EndpointReader>());
            services.AddSingleton<IChannelProvider, GrpcNetChannelProvider>();
        }

        private static GrpcServiceEndpointConventionBuilder AddProtoRemoteEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<HealthServiceImpl>();
            return endpoints.MapGrpcService<Remoting.RemotingBase>();
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedGrpcNetRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseRouting();
            applicationBuilder.UseEndpoints(c => AddProtoRemoteEndpoint(c));
        }

        public static IServiceCollection AddRemote(this IServiceCollection services, Func<IServiceProvider, GrpcNetRemoteConfig> configure)
        {
            services.AddSingleton(configure);
            AddAllServices(services);
            return services;
        }

 public static IServiceCollection AddRemote(
            this IServiceCollection services,
            GrpcNetRemoteConfig config
        )
        {
            services.AddSingleton(config);
            AddAllServices(services);
            return services;
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder, Action<GrpcServiceEndpointConventionBuilder> configure)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedGrpcNetRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseEndpoints(c => configure(AddProtoRemoteEndpoint(c)));
        }

public static IServiceCollection AddClientRemote(
            this IServiceCollection services,
            GrpcNetRemoteConfig config
        )
        {
            services.AddSingleton(config);
            services.TryAddSingleton<ActorSystem>();
            services.AddSingleton<IRemote, GrpcNetClientRemote>();
            services.AddSingleton<RemoteHostedService>();
            return services;
        }
    }
}
#endif