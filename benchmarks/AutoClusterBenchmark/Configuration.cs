// -----------------------------------------------------------------------
// <copyright file="Configuration.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using ClusterExperiment1.Messages;
using Grpc.Net.Client;
using Grpc.Net.Compression;
using Proto;
using Proto.Cluster;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.OpenTelemetry;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using CompressionLevel = System.IO.Compression.CompressionLevel;

namespace ClusterExperiment1;

public static class Configuration
{
    private const bool EnableTracing = false;

    private static ClusterConfig GetClusterConfig(
        IClusterProvider clusterProvider,
        IIdentityLookup identityLookup
    )
    {
        var helloProps = Props.FromProducer(() => new WorkerActor());
        return ClusterConfig
            .Setup("mycluster", clusterProvider, identityLookup)
            .WithClusterContextProducer(cluster => new DefaultClusterContext(cluster))
            .WithClusterKind("hello", helloProps)
            .WithGossipFanOut(3);
    }

    private static GrpcNetRemoteConfig GetRemoteConfig()
    {
        var portStr = Environment.GetEnvironmentVariable("PROTOPORT") ?? $"{RemoteConfigBase.AnyFreePort}";
        var port = int.Parse(portStr);
        var host = Environment.GetEnvironmentVariable("PROTOHOST") ?? RemoteConfigBase.Localhost;
        var advertisedHost = Environment.GetEnvironmentVariable("PROTOHOSTPUBLIC");

        var remoteConfig = GrpcNetRemoteConfig
            .BindTo(host, port)
            .WithAdvertisedHost(advertisedHost)
            .WithChannelOptions(new GrpcChannelOptions
                {
                    CompressionProviders = new[]
                    {
                        new GzipCompressionProvider(CompressionLevel.Fastest)
                    }
                }
            )
            .WithProtoMessages(MessagesReflection.Descriptor)
            .WithEndpointWriterMaxRetries(2);

        return remoteConfig;
    }

    private static InMemAgent Agent = null!;

    private static TestProviderOptions Options = new()
    {
        DeregisterCritical = TimeSpan.FromSeconds(10),
        RefreshTtl = TimeSpan.FromSeconds(5),
        ServiceTtl = TimeSpan.FromSeconds(3)
    };

    public static void ResetAgent()
    {
        Agent = new();
    }

    private static IClusterProvider ClusterProvider() => new TestProvider(Options, Agent);

    private static IIdentityLookup GetIdentityLookup() => new PartitionIdentityLookup(
        new PartitionConfig
        {
            RebalanceActivationsCompletionTimeout = TimeSpan.FromSeconds(5),
            GetPidTimeout = TimeSpan.FromSeconds(5),
            RebalanceRequestTimeout = TimeSpan.FromSeconds(1),
            Mode = PartitionIdentityLookup.Mode.Push,
        }
    );

    public static async Task<Cluster> SpawnMember()
    {
        var system = new ActorSystem(GetMemberActorSystemConfig()
        );
        system.EventStream.Subscribe<ClusterTopology>(e => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"M:{system.Id}-{system.Address}-ClusterTopology:{e.GetMembershipHashCode()}");
                Console.ResetColor();
            }
        );
        system.EventStream.Subscribe<LeaderElected>(e => {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"M:{system.Id}-{system.Address}-Leader:{e.Leader.Id}");
                Console.ResetColor();
            }
        );
        var clusterProvider = ClusterProvider();
        var identity = GetIdentityLookup();

        system.WithRemote(GetRemoteConfig()).WithCluster(GetClusterConfig(clusterProvider, identity));
        await system.Cluster().StartMemberAsync();
        return system.Cluster();
    }

    private static ActorSystemConfig GetMemberActorSystemConfig()
    {
        var config = new ActorSystemConfig()
            .WithSharedFutures()
            .WithDeadLetterThrottleCount(3)
            .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1))
            .WithDeadLetterRequestLogging(false);

        return config;
    }

    public static async Task<Cluster> SpawnClient()
    {
        var config = new ActorSystemConfig().WithDeadLetterThrottleCount(3)
            .WithSharedFutures()
            .WithDeadLetterThrottleInterval(TimeSpan.FromSeconds(1))
            .WithDeadLetterRequestLogging(false);
        var system = new ActorSystem(EnableTracing ? config.WithConfigureProps(props => props.WithTracing()) : config);
        system.EventStream.Subscribe<ClusterTopology>(e => {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"C:{system.Id}-{system.Address}-ClusterTopology:{e.GetMembershipHashCode()}");
                Console.ResetColor();
            }
        );
        system.EventStream.Subscribe<LeaderElected>(e => {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"C:{system.Id}-{system.Address}-Leader:{e.Leader.Id}");
                Console.ResetColor();
            }
        );
        var clusterProvider = ClusterProvider();
        var identity = GetIdentityLookup();
        system.WithRemote(GetRemoteConfig()).WithCluster(GetClusterConfig(clusterProvider, identity));

        await system.Cluster().StartClientAsync();
        return system.Cluster();
    }
}