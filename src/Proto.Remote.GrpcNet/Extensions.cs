using System;
using Grpc.HealthCheck;
using Grpc.Net.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Proto.Remote.GrpcNet
{
    [PublicAPI]
    public static class Extensions
    {
        public static GrpcNetRemoteConfig WithChannelOptions(this GrpcNetRemoteConfig config, GrpcChannelOptions options)
            => config with {ChannelOptions = options};

        public static ActorSystem WithRemote(this ActorSystem system, GrpcNetRemoteConfig remoteConfig)
        {
            var _ = new GrpcNetRemote(system, remoteConfig);
            return system;
        }

        public static IServiceCollection AddRemote(this IServiceCollection services, Func<IServiceProvider, GrpcNetRemoteConfig> configure)
        {
            services.AddSingleton(sp => configure(sp));
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
            applicationBuilder.UseEndpoints(c => AddProtoRemoteEndpoint(c));
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder, Action<GrpcServiceEndpointConventionBuilder> configure)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedGrpcNetRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseEndpoints(c => configure(AddProtoRemoteEndpoint(c)));
        }
    }
}