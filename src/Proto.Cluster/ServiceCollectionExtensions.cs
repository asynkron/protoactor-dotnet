using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.DependencyInjection;
using Proto.Remote.GrpcNet;

namespace Proto.Cluster;

[PublicAPI]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtoCluster(this IServiceCollection self, string clusterName,
        string bindToHost = "localhost", int port = 0,
        Func<ActorSystemConfig, ActorSystemConfig>? configureSystem = null,
        Func<GrpcNetRemoteConfig, GrpcNetRemoteConfig>? configureRemote = null,
        Func<ClusterConfig, ClusterConfig>? configureCluster = null,
        IClusterProvider? clusterProvider = null,
        IIdentityLookup? identityLookup = null,
        bool runAsClient = false
    )
    {
        self.AddSingleton(p =>
        {
            var loggerFactory = p.GetRequiredService<ILoggerFactory>();
            Log.SetLoggerFactory(loggerFactory);

            var s = new ActorSystemConfig();
            s = configureSystem?.Invoke(s) ?? s;

            var r = GrpcNetRemoteConfig.BindTo(bindToHost, port);
            r = configureRemote?.Invoke(r) ?? r;
            clusterProvider ??= new SeedNodeClusterProvider();
            identityLookup ??= new PartitionIdentityLookup();

            var c = ClusterConfig.Setup(clusterName, clusterProvider, identityLookup);
            c = configureCluster?.Invoke(c) ?? c;

            var system = new ActorSystem(s)
                .WithRemote(r)
                .WithCluster(c)
                .WithServiceProvider(p);

            return system;
        });

        self.AddSingleton(p => p.GetRequiredService<ActorSystem>().Cluster());
        self.AddSingleton(p => p.GetRequiredService<ActorSystem>().Root);
        self.AddHostedService(p => new ProtoActorLifecycleHost(p.GetRequiredService<Cluster>(), runAsClient));

        return self;
    }
}