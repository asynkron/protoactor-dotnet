using System;
using System.Net;
using Grpc.HealthCheck;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Proto.Remote
{
    public static class Extensions
    {
        public static ActorSystem WithRemote(this ActorSystem system, RemoteConfig remoteConfig)
        {
            var _ = new Remote(system, remoteConfig);
            return system;
        }

        public static IServiceCollection AddRemote(this IServiceCollection services, Func<IServiceProvider, RemoteConfig> configure)
        {
            services.AddSingleton(sp => configure(sp));
            AddAllServices(services);
            return services;
        }

        public static IServiceCollection AddRemote(this IServiceCollection services,
            RemoteConfig config)
        {
            services.AddSingleton(config);
            AddAllServices(services);
            return services;
        }

        private static void AddAllServices(IServiceCollection services)
        {
            services.TryAddSingleton<ActorSystem>();
            services.AddHostedService<RemoteHostedService>();
            services.AddSingleton<HostedRemote>();
            services.AddSingleton<IRemote, HostedRemote>(sp => sp.GetRequiredService<HostedRemote>());
            services.AddSingleton<EndpointManager>();
            services.AddSingleton<RemoteConfigBase, RemoteConfig>(sp => sp.GetRequiredService<RemoteConfig>());
            services.AddSingleton<EndpointReader, EndpointReader>();
            services.AddSingleton<Serialization>(sp => sp.GetRequiredService<RemoteConfig>().Serialization);
            services.AddSingleton<Remoting.RemotingBase, EndpointReader>(sp => sp.GetRequiredService<EndpointReader>());
            services.AddSingleton<IChannelProvider, ChannelProvider>();
        }

        private static GrpcServiceEndpointConventionBuilder AddProtoRemoteEndpoint(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<HealthServiceImpl>();
            return endpoints.MapGrpcService<Remoting.RemotingBase>();
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseEndpoints(c => AddProtoRemoteEndpoint(c));
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder, Action<GrpcServiceEndpointConventionBuilder> configure)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseEndpoints(c => configure(AddProtoRemoteEndpoint(c)));
        }
    }
}