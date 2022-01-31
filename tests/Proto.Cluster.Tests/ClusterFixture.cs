#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Proto.Cluster.Cache;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Logging;
using Proto.OpenTelemetry;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Xunit;

// ReSharper disable ClassNeverInstantiated.Global

namespace Proto.Cluster.Tests
{
    public interface IClusterFixture
    {
        IList<Cluster> Members { get; }

        public Task<Cluster> SpawnNode();

        LogStore LogStore { get; }

        Task RemoveNode(Cluster member, bool graceful = true);
    }

    public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture, IAsyncDisposable
    {
        private const bool EnableTracing = false;

        protected readonly string ClusterName;
        private readonly int _clusterSize;
        private readonly Func<ClusterConfig, ClusterConfig>? _configure;
        private readonly ILogger _logger = Log.CreateLogger(nameof(GetType));
        private readonly TracerProvider? _tracerProvider;
        private readonly List<Cluster> _members = new();

        protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig>? configure = null)
        {
            _clusterSize = clusterSize;
            _configure = configure;
            ClusterName = $"test-cluster-{Guid.NewGuid().ToString().Substring(0, 6)}";

#pragma warning disable CS0162
            // ReSharper disable once HeuristicUnreachableCode
            if (EnableTracing)
            {
                _tracerProvider = InitOpenTelemetryTracing();
            }
#pragma warning restore CS0162
        }

        private static TracerProvider InitOpenTelemetryTracing() => global::OpenTelemetry.Sdk.CreateTracerProviderBuilder()
            .SetResourceBuilder(ResourceBuilder.CreateDefault()
                .AddService("Proto.Cluster.Tests")
            )
            .AddProtoActorInstrumentation()
            .AddJaegerExporter(options => options.AgentHost = "localhost")
            .Build();

        protected virtual ClusterKind[] ClusterKinds => new[]
        {
            new ClusterKind(EchoActor.Kind, EchoActor.Props.WithClusterRequestDeduplication()),
            new ClusterKind(EchoActor.Kind2, EchoActor.Props),
            new ClusterKind(EchoActor.LocalAffinityKind, EchoActor.Props).WithLocalAffinityRelocationStrategy(),
        };

        public async Task InitializeAsync()
        {
            var nodes = await SpawnClusterNodes(_clusterSize, _configure).ConfigureAwait(false);
            _members.AddRange(nodes);
        }

        public LogStore LogStore { get; } = new();

        public async Task DisposeAsync()
        {
            try
            {
                _tracerProvider?.Dispose();
                await Task.WhenAll(Members.ToList().Select(cluster => cluster.ShutdownAsync())).ConfigureAwait(false);
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to shutdown gracefully");
                throw;
            }
        }

        public async Task RemoveNode(Cluster member, bool graceful = true)
        {
            if (Members.Contains(member))
            {
                Members.Remove(member);
                await member.ShutdownAsync(graceful).ConfigureAwait(false);
            }
            else throw new ArgumentException("No such member");
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

        private async Task<IList<Cluster>> SpawnClusterNodes(
            int count,
            Func<ClusterConfig, ClusterConfig>? configure = null
        ) => (await Task.WhenAll(
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
                .WithClusterKinds(ClusterKinds);

            config = configure?.Invoke(config) ?? config;

            var system = new ActorSystem(GetActorSystemConfig());
            system.Extensions.Register(new InstanceLogger(LogLevel.Debug, LogStore, category: system.Id));

            var logger = system.Logger()?.BeginScope<EventStream>();
            system.EventStream.Subscribe<object>(e => { logger?.LogDebug("EventStream {MessageType}:{Message}", e.GetType().Name, e); }
            );

            var remoteConfig = GrpcCoreRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
            var _ = new GrpcCoreRemote(system, remoteConfig);

            var cluster = new Cluster(system, config);

            await cluster.StartMemberAsync();
            return cluster;
        }

        protected virtual ActorSystemConfig GetActorSystemConfig()
        {
            var actorSystemConfig = ActorSystemConfig.Setup();

            // ReSharper disable once HeuristicUnreachableCode
            return EnableTracing ? actorSystemConfig.WithConfigureProps(props => props.WithTracing()) : actorSystemConfig;
        }

        protected abstract IClusterProvider GetClusterProvider();

        protected virtual IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionIdentityLookup(
            new PartitionConfig
            {
                RebalanceActivationsCompletionTimeout = TimeSpan.FromSeconds(3),
                GetPidTimeout = TimeSpan.FromSeconds(2),
                HandoverChunkSize = 1000,
                RebalanceRequestTimeout = TimeSpan.FromSeconds(1),
                Mode = PartitionIdentityLookup.Mode.Pull
        });

        async ValueTask IAsyncDisposable.DisposeAsync() => await DisposeAsync();
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

    // ReSharper disable once ClassNeverInstantiated.Global
    public class InMemoryClusterFixture : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixture() : base(3, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
        {
        }
    }

    public class InMemoryClusterFixtureAlternativeClusterContext : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixtureAlternativeClusterContext() : base(3, config => config
            .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
            .WithClusterContextProducer(cluster => new ExperimentalClusterContext(cluster))
        )
        {
        }
    }

    public class InMemoryClusterFixtureSharedFutures : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixtureSharedFutures() : base(3, config => config
            .WithActorRequestTimeout(TimeSpan.FromSeconds(4))
            .WithClusterContextProducer(cluster => new ExperimentalClusterContext(cluster))
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

        protected override ClusterKind[] ClusterKinds => base.ClusterKinds.Select(ck => ck.WithPidCacheInvalidation()).ToArray();

        protected override async Task<Cluster> SpawnClusterMember(Func<ClusterConfig, ClusterConfig>? configure)
        {
            var cluster = await base.SpawnClusterMember(configure);
            return cluster.WithPidCacheInvalidation();
        }
    }
}