using System.IO.Compression;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Partition;
using Proto.DependencyInjection;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using TestRunner.ProtoActor;
using TestRunner.Tests;

namespace ProtoActorSut.Shared;

public static class ProtoActorExtensions
{
    public static WebApplicationBuilder AddProtoActorTestServicesRaw(this WebApplicationBuilder builder)
    {
        builder.Services.AddSingleton<ProtoActorTestServicesRaw>();
        builder.Services.AddSingleton<Ping>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Ping);
        builder.Services.AddSingleton<Activate>(provider => provider.GetRequiredService<ProtoActorTestServicesRaw>().Activate);

        return builder;
    }
    
    public static WebApplicationBuilder AddProtoActor(this WebApplicationBuilder builder,
        params (string Kind, Props Props)[] kinds)
    {
        builder.Services.AddSingleton(provider =>
        {
            var config = builder.Configuration.GetSection("ProtoActor");

            Log.SetLoggerFactory(provider.GetRequiredService<ILoggerFactory>());

            var actorSystemConfig = ActorSystemConfig
                .Setup()
                .WithSharedFutures(2000)
                .WithDeadLetterThrottleCount(3)
                .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1));

            var system = new ActorSystem(actorSystemConfig);

            var (remoteConfig, clusterProvider) =
                ConfigureForLocalhost();

            var clusterConfig = ClusterConfig
                .Setup(config["ClusterName"], clusterProvider, new PartitionIdentityLookup());

            foreach (var kind in kinds)
            {
                clusterConfig = clusterConfig.WithClusterKind(kind.Kind, kind.Props);
            }
            
            system
                .WithServiceProvider(provider)
                .WithRemote(remoteConfig)
                .WithCluster(clusterConfig)
                .Cluster();

            return system;
        });

        builder.Services.AddSingleton(provider => provider.GetRequiredService<ActorSystem>().Cluster());
        builder.Services.AddHostedService<ActorSystemHostedService>();

        return builder;
    }

    static (GrpcNetRemoteConfig, IClusterProvider) ConfigureForLocalhost()
        => (GrpcNetRemoteConfig.BindToLocalhost()
                .WithProtoMessages(Contracts.ProtosReflection.Descriptor)
                .WithLogLevelForDeserializationErrors(LogLevel.Critical),
            new ConsulProvider(new ConsulProviderConfig()));
}