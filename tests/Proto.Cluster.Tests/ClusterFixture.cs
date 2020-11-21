namespace Proto.Cluster.Tests
{
    using System;
    using System.Collections.Immutable;
    using System.Linq;
    using System.Threading.Tasks;
    using ClusterTest.Messages;
    using IdentityLookup;
    using Partition;
    using Remote;
    using Testing;
    using Xunit;
    using Proto.Remote.GrpcCore;

    public interface IClusterFixture
    {
        ImmutableList<Cluster> Members { get; }
    }

    public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture
    {
        public ImmutableList<Cluster> Members { get; private set; }

        private readonly int _clusterSize;
        private readonly Func<ClusterConfig, ClusterConfig> _configure;

        protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null)
        {
            _clusterSize = clusterSize;
            _configure = configure;
        }

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

        protected virtual (string, Props)[] ClusterKinds => new[] {
            (EchoActor.Kind, EchoActor.Props),
            (EchoActor.Kind2, EchoActor.Props),
        };

        public async Task InitializeAsync()
        {
            Members = await SpawnClusterNodes(_clusterSize, _configure);
        }

        public async Task DisposeAsync()
        {
            try {
                await Task.WhenAll(Members?.Select(cluster => cluster.ShutdownAsync()) ?? new[] {Task.CompletedTask});
            }
            catch (Exception e) {
                Console.WriteLine(e);
                throw;
            }
        }
    }

    public abstract class BaseInMemoryClusterFixture : ClusterFixture
    {
        private readonly Lazy<InMemAgent> _inMemAgent = new(() => new InMemAgent());
        private InMemAgent InMemAgent => _inMemAgent.Value;

        protected BaseInMemoryClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null) :
            base(
                clusterSize,
                configure
            )
        {
        }

        protected override IClusterProvider GetClusterProvider() => new TestProvider(new TestProviderOptions(), InMemAgent);
    }

    // ReSharper disable once ClassNeverInstantiated.Global
    public class InMemoryClusterFixture : BaseInMemoryClusterFixture
    {
        public InMemoryClusterFixture() : base(3)
        {
        }
    }
}