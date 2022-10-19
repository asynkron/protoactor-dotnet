﻿#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Proto.Cluster.Cache;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.PartitionActivator;
using Proto.Cluster.SingleNode;
using Proto.Cluster.Testing;
using Proto.Logging;
using Proto.OpenTelemetry;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace Proto.Cluster.Tests;

public interface IClusterFixture
{
    IList<Cluster> Members { get; }

    LogStore LogStore { get; }
    int ClusterSize { get; }

    public Task<Cluster> SpawnNode();

    Task RemoveNode(Cluster member, bool graceful = true);
}

public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture, IAsyncDisposable
{
    private const bool EnableTracing = false;
    public const string InvalidIdentity = "invalid";
    private readonly Func<ClusterConfig, ClusterConfig>? _configure;
    private readonly ILogger _logger = Log.CreateLogger(nameof(GetType));
    private readonly List<Cluster> _members = new();
    private readonly TracerProvider? _tracerProvider;

    protected readonly string ClusterName;

    protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig>? configure = null)
    {
#if NETCOREAPP3_1
        AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
#endif
        ClusterSize = clusterSize;
        _configure = configure;
        ClusterName = $"test-cluster-{Guid.NewGuid().ToString().Substring(0, 6)}";

        //TODO: check if this helps low resource envs like github actions.
        ThreadPool.SetMaxThreads(100, 100);

#pragma warning disable CS0162
        // ReSharper disable once HeuristicUnreachableCode
        if (EnableTracing)
        {
            _tracerProvider = InitOpenTelemetryTracing();
        }
#pragma warning restore CS0162
    }

    protected virtual ClusterKind[] ClusterKinds => new[]
    {
        new ClusterKind(EchoActor.Kind, EchoActor.Props.WithClusterRequestDeduplication()),
        new ClusterKind(EchoActor.Kind2, EchoActor.Props),
        new ClusterKind(EchoActor.LocalAffinityKind, EchoActor.Props).WithLocalAffinityRelocationStrategy(),
        new ClusterKind(EchoActor.FilteredKind, EchoActor.Props).WithSpawnPredicate((identity, _)
            => new ValueTask<bool>(!identity.Equals(InvalidIdentity, StringComparison.InvariantCultureIgnoreCase))
        ),
        new ClusterKind(EchoActor.AsyncFilteredKind, EchoActor.Props).WithSpawnPredicate(async (identity, ct) =>
            {
                await Task.Delay(100, ct);

                return !identity.Equals(InvalidIdentity, StringComparison.InvariantCultureIgnoreCase);
            }
        )
    };

    async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();

    public async Task InitializeAsync()
    {
        var nodes = await SpawnClusterNodes(ClusterSize, _configure).ConfigureAwait(false);
        _members.AddRange(nodes);
    }

    public async Task DisposeAsync()
    {
        try
        {
            await OnDisposing();
            _tracerProvider?.Dispose();
            await Task.WhenAll(Members.ToList().Select(cluster => cluster.ShutdownAsync())).ConfigureAwait(false);
            Members.Clear(); // prevent multiple shutdown attempts if dispose is called multiple times
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Failed to shutdown gracefully");

            throw;
        }
    }

    public int ClusterSize { get; }

    public LogStore LogStore { get; } = new();

    public async Task RemoveNode(Cluster member, bool graceful = true)
    {
        if (Members.Contains(member))
        {
            Members.Remove(member);
            await member.ShutdownAsync(graceful, "Stopped by ClusterFixture").ConfigureAwait(false);
        }
        else
        {
            throw new ArgumentException("No such member");
        }
    }

    /// <summary>
    ///     Spawns a node, adds it to the cluster and member list
    /// </summary>
    /// <returns></returns>
    /// <exception cref="ArgumentException"></exception>
    public async Task<Cluster> SpawnNode()
    {
        var newMember = await SpawnClusterMember(_configure);
        Members.Add(newMember);

        return newMember;
    }

    public IList<Cluster> Members => _members;

    private static TracerProvider InitOpenTelemetryTracing() =>
        Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("Proto.Cluster.Tests")
            )
            .AddProtoActorInstrumentation()
            .AddSource(Tracing.ActivitySourceName)
            .AddOtlpExporter(options => options.Endpoint = new Uri("http://localhost:4317"))
            .Build();

    public virtual Task OnDisposing() => Task.CompletedTask;

    private async Task<IList<Cluster>> SpawnClusterNodes(
        int count,
        Func<ClusterConfig, ClusterConfig>? configure = null
    ) =>
        (await Task.WhenAll(
            Enumerable.Range(0, count)
                .Select(_ => SpawnClusterMember(configure))
        )).ToList();

    protected virtual async Task<Cluster> SpawnClusterMember(Func<ClusterConfig, ClusterConfig>? configure)
    {
        var config = ClusterConfig.Setup(
                ClusterName,
                GetClusterProvider(),
                GetIdentityLookup(ClusterName)
            )
            .WithClusterKinds(ClusterKinds)
            .WithHeartbeatExpiration(TimeSpan.Zero);

        config = configure?.Invoke(config) ?? config;

        var system = new ActorSystem(GetActorSystemConfig());
        system.Extensions.Register(new InstanceLogger(LogLevel.Debug, LogStore, category: system.Id));

        var logger = system.Logger()?.BeginScope<EventStream>();

        system.EventStream.Subscribe<object>(e =>
            {
                logger?.LogDebug("EventStream {MessageType}:{Message}", e.GetType().Name, e);
            }
        );

        var remoteConfig = GrpcNetRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
        var _ = new GrpcNetRemote(system, remoteConfig);

        var cluster = new Cluster(system, config);

        await cluster.StartMemberAsync();

        return cluster;
    }

    protected virtual ActorSystemConfig GetActorSystemConfig()
    {
        var actorSystemConfig = ActorSystemConfig.Setup();

        // ReSharper disable once HeuristicUnreachableCode
        return EnableTracing
            ? actorSystemConfig
                .WithConfigureProps(props => props.WithTracing())
                .WithConfigureRootContext(context => context.WithTracing())
            : actorSystemConfig;
    }

    protected abstract IClusterProvider GetClusterProvider();

    protected virtual IIdentityLookup GetIdentityLookup(string clusterName) =>
        new PartitionIdentityLookup(
            new PartitionConfig
            {
                RebalanceActivationsCompletionTimeout = TimeSpan.FromSeconds(3),
                GetPidTimeout = TimeSpan.FromSeconds(2),
                HandoverChunkSize = 1000,
                RebalanceRequestTimeout = TimeSpan.FromSeconds(1),
                Mode = PartitionIdentityLookup.Mode.Pull
            }
        );
}

public abstract class BaseInMemoryClusterFixture : ClusterFixture
{
    private readonly Lazy<InMemAgent> _inMemAgent = new(() => new InMemAgent());

    protected BaseInMemoryClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig>? configure = null) :
        base(
            clusterSize,
            configure
        )
    {
    }

    private InMemAgent InMemAgent => _inMemAgent.Value;

    protected override IClusterProvider GetClusterProvider() => new TestProvider(new TestProviderOptions(), InMemAgent);
}

public class InMemoryClusterFixture : BaseInMemoryClusterFixture
{
    public InMemoryClusterFixture() : base(3, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
    {
    }
}

public class InMemoryClusterFixtureWithPartitionActivator : BaseInMemoryClusterFixture
{
    public InMemoryClusterFixtureWithPartitionActivator() : base(3,
        config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
    {
    }

    protected override IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionActivatorLookup();
}

public class InMemoryClusterFixtureAlternativeClusterContext : BaseInMemoryClusterFixture
{
    public InMemoryClusterFixtureAlternativeClusterContext() : base(3, config => config
        .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
        .WithClusterContextProducer(cluster => new DefaultClusterContext(cluster))
    )
    {
    }
}

public class InMemoryClusterFixtureSharedFutures : BaseInMemoryClusterFixture
{
    public InMemoryClusterFixtureSharedFutures() : base(3, config => config
        .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
        .WithClusterContextProducer(cluster => new DefaultClusterContext(cluster))
    )
    {
    }

    protected override ActorSystemConfig GetActorSystemConfig() => base.GetActorSystemConfig().WithSharedFutures();
}

public class InMemoryPidCacheInvalidationClusterFixture : BaseInMemoryClusterFixture
{
    public InMemoryPidCacheInvalidationClusterFixture() : base(3, config => config
        .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
    )
    {
    }

    protected override ClusterKind[] ClusterKinds =>
        base.ClusterKinds.Select(ck => ck.WithPidCacheInvalidation()).ToArray();

    protected override async Task<Cluster> SpawnClusterMember(Func<ClusterConfig, ClusterConfig>? configure)
    {
        var cluster = await base.SpawnClusterMember(configure);

        return cluster.WithPidCacheInvalidation();
    }
}

public class SingleNodeProviderFixture : ClusterFixture
{
    public SingleNodeProviderFixture() : base(1, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
    {
    }

    protected override IClusterProvider GetClusterProvider() => new SingleNodeProvider();

    protected override IIdentityLookup GetIdentityLookup(string clusterName) => new SingleNodeLookup();
}