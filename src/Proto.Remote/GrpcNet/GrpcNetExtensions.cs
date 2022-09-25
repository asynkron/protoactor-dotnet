using System;
using System.Collections.Generic;
using Grpc.HealthCheck;
using Grpc.Net.Client;
using JetBrains.Annotations;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Proto.Remote.GrpcNet;

[PublicAPI]
public static class Extensions
{
    /// <summary>
    ///     Channel options for the gRPC channel
    /// </summary>
    public static GrpcNetRemoteConfig WithChannelOptions(this GrpcNetRemoteConfig config, GrpcChannelOptions options) =>
        config with { ChannelOptions = options };

    /// <summary>
    ///     A delegate that allows to choose the address for the <see cref="ActorSystem" /> from the list of addresses Kestrel
    ///     listens on.
    ///     By default, the first address is used.
    /// </summary>
    public static GrpcNetRemoteConfig WithUriChooser(this GrpcNetRemoteConfig config,
        Func<IEnumerable<Uri>?, Uri?> uriChooser) =>
        config with { UriChooser = uriChooser };

    /// <summary>
    ///     Registers the Remote extension in the <see cref="ActorSystem" />. This mode opens connections both ways between the
    ///     nodes.
    ///     Use this mode as a default.
    /// </summary>
    /// <param name="system"></param>
    /// <param name="remoteConfig">Remote extension config</param>
    /// <returns></returns>
    public static ActorSystem WithRemote(this ActorSystem system, GrpcNetRemoteConfig remoteConfig)
    {
        var _ = new GrpcNetRemote(system, remoteConfig);

        return system;
    }

    /// <summary>
    ///     Registers the Remote extension in the <see cref="ActorSystem" /> This mode marks the remote as a system that cannot
    ///     be connected to.
    ///     However this system can connect to remote node. Use in the cases where a node is behind a firewall, so other nodes
    ///     cannot connect to it.
    /// </summary>
    /// <param name="system"></param>
    /// <param name="remoteConfig">Remote extension config</param>
    /// <returns></returns>
    public static ActorSystem WithClientRemote(this ActorSystem system, GrpcNetRemoteConfig remoteConfig)
    {
        var _ = new GrpcNetClientRemote(system, remoteConfig);

        return system;
    }

    internal static IServiceCollection AddRemote(this IServiceCollection services,
        Func<IServiceProvider, GrpcNetRemoteConfig> configure)
    {
        services.AddSingleton(configure);
        AddAllServices(services);

        return services;
    }

    internal static IServiceCollection AddRemote(
        this IServiceCollection services,
        GrpcNetRemoteConfig config
    )
    {
        services.AddSingleton(config);
        AddAllServices(services);

        return services;
    }

    internal static IServiceCollection AddClientRemote(
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

    private static void AddAllServices(IServiceCollection services)
    {
        services.TryAddSingleton<ActorSystem>();
        services.AddHostedService<RemoteHostedService>();
        services.AddSingleton<HostedGrpcNetRemote>();
        services.AddSingleton<IRemote, HostedGrpcNetRemote>(sp => sp.GetRequiredService<HostedGrpcNetRemote>());
        services.AddSingleton<EndpointManager>();

        services.AddSingleton<RemoteConfigBase, GrpcNetRemoteConfig>(sp =>
            sp.GetRequiredService<GrpcNetRemoteConfig>());

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

    internal static void UseProtoRemote(this IApplicationBuilder applicationBuilder)
    {
        var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedGrpcNetRemote>();
        hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
        applicationBuilder.UseRouting();
        applicationBuilder.UseEndpoints(c => AddProtoRemoteEndpoint(c));
    }

    internal static void UseProtoRemote(this IApplicationBuilder applicationBuilder,
        Action<GrpcServiceEndpointConventionBuilder> configure)
    {
        var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedGrpcNetRemote>();
        hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
        applicationBuilder.UseEndpoints(c => configure(AddProtoRemoteEndpoint(c)));
    }
}