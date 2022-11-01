using System;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Seed;
using Proto.DependencyInjection;
using Proto.Remote.GrpcNet;

namespace Proto.Cluster;

[PublicAPI]
public class HostedClusterConfig
{
    public string ClusterName { get; set; } = "MyCluster";
    public string BindToHost { get; set; }= "localhost";
    public int Port{ get; set; } = 0;
    public Func<ActorSystemConfig, ActorSystemConfig>? ConfigureSystem { get; set; }
    public Func<GrpcNetRemoteConfig, GrpcNetRemoteConfig>? ConfigureRemote { get; set; }
    public Func<ClusterConfig, ClusterConfig>? ConfigureCluster { get; set; }
    public IClusterProvider? ClusterProvider { get; set; }
    public IIdentityLookup? IdentityLookup { get; set; }
    public bool RunAsClient { get; set; }
}

[PublicAPI]
public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddProtoCluster(this IServiceCollection self, Action<IServiceProvider, HostedClusterConfig> configure)
    {
        var boot = new HostedClusterConfig();
        self.AddSingleton(p =>
        {
            var loggerFactory = p.GetRequiredService<ILoggerFactory>();
            Log.SetLoggerFactory(loggerFactory);

            configure(p, boot);

            var s = new ActorSystemConfig();
            s = boot.ConfigureSystem?.Invoke(s) ?? s;

            var r = GrpcNetRemoteConfig.BindTo(boot.BindToHost, boot.Port);
            r = boot.ConfigureRemote?.Invoke(r) ?? r;
            boot.ClusterProvider ??= new SeedNodeClusterProvider();
            boot.IdentityLookup ??= new PartitionIdentityLookup();

            var c = ClusterConfig.Setup(boot.ClusterName, boot.ClusterProvider, boot.IdentityLookup);
            c = boot.ConfigureCluster?.Invoke(c) ?? c;

            var system = new ActorSystem(s)
                .WithRemote(r)
                .WithCluster(c)
                .WithServiceProvider(p);

            return system;
        });

        self.AddSingleton(p => p.GetRequiredService<ActorSystem>().Cluster());
        self.AddSingleton(p => p.GetRequiredService<ActorSystem>().Root);
        self.AddHostedService(p =>
            new ProtoActorLifecycleHost(
                p.GetRequiredService<ActorSystem>(),
                p.GetRequiredService<IHostApplicationLifetime>(),
                boot.RunAsClient));

        return self;
    }

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
        self.AddHostedService(p =>
            new ProtoActorLifecycleHost(
                p.GetRequiredService<ActorSystem>(),
                p.GetRequiredService<IHostApplicationLifetime>(),
                runAsClient));

        return self;
    }
}
