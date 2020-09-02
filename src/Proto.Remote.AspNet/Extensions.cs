// -----------------------------------------------------------------------
//   <copyright file="Extensions.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using Grpc.HealthCheck;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Proto.Remote
{
    public static class Extensions
    {
        public static IRemote AddRemote(this ActorSystem actorSystem, int port = 0,
            Action<RemoteConfiguration>? configure = null)
        {
            var remote = new SelfHostedRemote(actorSystem, IPAddress.Any, port, configure);
            return remote;
        }
        public static IRemote AddRemote(this ActorSystem actorSystem, IPAddress ipAddress, int port = 0,
            Action<RemoteConfiguration>? configure = null)
        {
            var remote = new SelfHostedRemote(actorSystem, ipAddress, port, configure);
            return remote;
        }
        public static IServiceCollection AddRemote(this IServiceCollection services,
            Action<RemoteConfiguration, IServiceProvider> configure)
        {
            services.AddSingleton<RemoteConfiguration>(sp =>
            {
                var remoteConfig = sp.GetRequiredService<AspRemoteConfig>();
                var serialization = sp.GetRequiredService<Serialization>();
                var remoteKindRegistry = sp.GetRequiredService<RemoteKindRegistry>();
                var remoteConfiguration = new RemoteConfiguration(serialization, remoteKindRegistry, remoteConfig);
                configure.Invoke(remoteConfiguration, sp);
                return remoteConfiguration;
            }
            );
            AddAllServices(services);
            return services;
        }

        public static IServiceCollection AddRemote(this IServiceCollection services,
            Action<RemoteConfiguration> configure)
        {
            services.AddSingleton<RemoteConfiguration>(sp =>
                 {
                     var remoteConfig = sp.GetRequiredService<AspRemoteConfig>();
                     var serialization = sp.GetRequiredService<Serialization>();
                     var remoteKindRegistry = sp.GetRequiredService<RemoteKindRegistry>();
                     var remoteConfiguration = new RemoteConfiguration(serialization, remoteKindRegistry, remoteConfig);
                     configure.Invoke(remoteConfiguration);
                     return remoteConfiguration;
                 }
             );
            AddAllServices(services);
            return services;
        }

        private static void AddAllServices(IServiceCollection services)
        {
            services.TryAddSingleton<ActorSystem>();
            services.AddHostedService<RemoteHostedService>();
            services.AddSingleton<Remote, Remote>();
            services.AddSingleton<HostedRemote, HostedRemote>();
            services.AddSingleton<IRemote, HostedRemote>(sp =>
            {
                sp.GetRequiredService<RemoteConfiguration>();
                return sp.GetRequiredService<HostedRemote>();
            });
            services.AddSingleton<EndpointManager>();
            services.AddSingleton<Serialization>();
            services.AddSingleton<RemoteKindRegistry>();
            services.AddSingleton<RemoteConfig, AspRemoteConfig>(sp => sp.GetRequiredService<AspRemoteConfig>());
            services.AddSingleton<AspRemoteConfig>();
            services.AddSingleton<EndpointReader, EndpointReader>();
            services.AddSingleton<Remoting.RemotingBase, EndpointReader>(sp => sp.GetRequiredService<EndpointReader>());
            services.AddSingleton<IChannelProvider, ChannelProvider>();
        }

        private static GrpcServiceEndpointConventionBuilder MapProtoRemoteService(IEndpointRouteBuilder endpoints)
        {
            endpoints.MapGrpcService<HealthServiceImpl>();
            return endpoints.MapGrpcService<Remoting.RemotingBase>();
        }

        public static void UseProtoRemote(this IApplicationBuilder applicationBuilder)
        {
            var hostedRemote = applicationBuilder.ApplicationServices.GetRequiredService<HostedRemote>();
            hostedRemote.ServerAddressesFeature = applicationBuilder.ServerFeatures.Get<IServerAddressesFeature>();
            applicationBuilder.UseEndpoints(c => MapProtoRemoteService(c));
        }
    }
}