using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Xunit;

namespace Proto.Cluster.Tests
{
    public interface IClusterFixture
    {
        ImmutableList<Cluster> Members { get; }
    }

    public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture
    {
        private readonly int _clusterSize;
        private readonly Func<ClusterConfig, ClusterConfig> _configure;
        private readonly ILogger _logger = Log.CreateLogger(nameof(GetType));

        protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null)
        {
            _clusterSize = clusterSize;
            _configure = configure;
        }

        protected virtual (string, Props)[] ClusterKinds => new[]
        {
            (EchoActor.Kind, EchoActor.Props),
            (EchoActor.Kind2, EchoActor.Props)
        };

        public async Task InitializeAsync()
        {
            Members = await SpawnClusterNodes(_clusterSize, _configure);
        }

        public async Task DisposeAsync()
        {
            try
            {
                await Task.WhenAll(Members?.Select(cluster => cluster.ShutdownAsync()) ?? new[] {Task.CompletedTask});
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to shutdown gracefully");
                throw;
            }
        }

        public ImmutableList<Cluster> Members { get; private set; }

        private async Task<ImmutableList<Cluster>> SpawnClusterNodes(
            int count,
            Func<ClusterConfig, ClusterConfig> configure = null
        )
        {
            var clusterName = $"test-cluster-{count}";

            return (await Task.WhenAll(
                Enumerable.Range(0, count)
                    .Select(_ => SpawnClusterMember(configure, clusterName))
            )).ToImmutableList();
        }

        protected virtual async Task<Cluster> SpawnClusterMember(
            Func<ClusterConfig, ClusterConfig> configure,
            string clusterName
        )
        {
            var config = ClusterConfig.Setup(
                    clusterName,
                    GetClusterProvider(),
                    GetIdentityLookup(clusterName)
                )
                .WithClusterKinds(ClusterKinds);

            config = configure?.Invoke(config) ?? config;
            var system = new ActorSystem();

            var remoteConfig = GrpcCoreRemoteConfig.BindToLocalhost().WithProtoMessages(MessagesReflection.Descriptor);
            var _ = new GrpcCoreRemote(system, remoteConfig);

            var cluster = new Cluster(system, config);

            await cluster.StartMemberAsync();
            return cluster;
        }

        protected abstract IClusterProvider GetClusterProvider();

        protected virtual IIdentityLookup GetIdentityLookup(string clusterName) => new PartitionIdentityLookup();
    }

    public abstract class BaseInMemoryClusterFixture : ClusterFixture
    {
        private readonly Lazy<InMemAgent> _inMemAgent = new(() => new InMemAgent());

        protected BaseInMemoryClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null) :
            base(
                clusterSize,
                configure
            )
        {
        }

        private InMemAgent InMemAgent => _inMemAgent.Value;

        protected override IClusterProvider GetClusterProvider() =>
            new TestProvider(new TestProviderOptions(), InMemAgent);
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class InMemoryClusterFixture : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixture() : base(3)
        {
        }
    }
}