using System.IO.Compression;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.DependencyInjection;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using ProtoActorSut.Shared;
using SkyriseMini.Tests;

namespace SkyriseMini;

public static class ProtoActorExtensions
{
    public static WebApplicationBuilder AddProtoActorTestServicesRaw(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ProtoActorTestServicesRaw>();
        builder.Services.AddSingleton<Ping>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Ping);
        builder.Services.AddSingleton<Activate>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Activate);

        return builder;
    }

    public static WebApplicationBuilder AddProtoActorClient(this WebApplicationBuilder builder)
    {
        
        builder.Services.AddSingleton(provider =>
        {
            var config = builder.Configuration.GetSection("ProtoActor");

            Log.SetLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

            var actorSystemConfig = ActorSystemConfig
                .Setup()
                .WithDeadLetterThrottleCount(3)
                .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1));

            var system = new ActorSystem(actorSystemConfig);

            var remoteConfig = GrpcNetRemoteConfig.BindToLocalhost()
                .WithProtoMessages(ProtoActorSut.Contracts.ProtosReflection.Descriptor)
                // .WithChannelOptions(new GrpcChannelOptions
                //     {
                //         CompressionProviders = new[]
                //         {
                //             new GzipCompressionProvider(CompressionLevel.Fastest)
                //         }
                //     }
                // )
                .WithLogLevelForDeserializationErrors(LogLevel.Critical);

            var clusterProvider = new ConsulProvider(new ConsulProviderConfig());

            var clusterConfig = ClusterConfig
                .Setup(config["ClusterName"]!, clusterProvider, new PartitionIdentityLookup());

            system
                .WithServiceProvider(provider)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig)
                .Cluster();

            return system;
        });
        
        builder.Services.AddHostedService<ActorSystemHostedService>();

        return builder;
    }
}