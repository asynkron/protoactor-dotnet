using System.IO.Compression;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Consul;
using Proto.Cluster.Kubernetes;
using Proto.Cluster.Partition;
using Proto.DependencyInjection;
using Proto.Remote;
using Proto.Remote.GrpcNet;

namespace ProtoActorSut.Shared;

public static class ProtoActorExtensions
{
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
                ConfigureClustering(config, useKubernetes: !builder.Environment.IsDevelopment());

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

    static (GrpcNetRemoteConfig, IClusterProvider)
        ConfigureClustering(IConfigurationSection config, bool useKubernetes) =>
        useKubernetes ? ConfigureForKubernetes(config) : ConfigureForLocalhost();

    static (GrpcNetRemoteConfig, IClusterProvider) ConfigureForKubernetes(IConfigurationSection config)
    {
        var clusterProvider = new KubernetesProvider(new KubernetesProviderConfig());

        var remoteConfig = GrpcNetRemoteConfig
            .BindToAllInterfaces(config["AdvertisedHost"], config.GetValue("AdvertisedPort", 0))
            .WithChannelOptions(new GrpcChannelOptions
                {
                    CompressionProviders = new[]
                    {
                        new GzipCompressionProvider(CompressionLevel.Fastest)
                    }
                }
            )
            .WithProtoMessages(Contracts.ProtosReflection.Descriptor)
            .WithLogLevelForDeserializationErrors(LogLevel.Critical);

        return (remoteConfig, clusterProvider);
    }

    static (GrpcNetRemoteConfig, IClusterProvider) ConfigureForLocalhost()
        => (GrpcNetRemoteConfig.BindToLocalhost()
                .WithProtoMessages(Contracts.ProtosReflection.Descriptor)
                .WithLogLevelForDeserializationErrors(LogLevel.Critical),
            new ConsulProvider(new ConsulProviderConfig()));
}