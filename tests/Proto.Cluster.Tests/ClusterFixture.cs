using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClusterTest.Messages;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Testing;
using Proto.Remote;
using Proto.Remote.GrpcCore;
using Xunit;

namespace Proto.Cluster.Tests
{
    public interface IClusterFixture
    {
        IList<Cluster> Members { get; }

        public Task<Cluster> SpawnNode();

        Task RemoveNode(Cluster member, bool graceful = true);
    }

    public abstract class ClusterFixture : IAsyncLifetime, IClusterFixture
    {
        protected readonly string _clusterName;
        private readonly int _clusterSize;
        private readonly Func<ClusterConfig, ClusterConfig> _configure;
        private readonly ILogger _logger = Log.CreateLogger(nameof(GetType));

        protected ClusterFixture(int clusterSize, Func<ClusterConfig, ClusterConfig> configure = null)
        {
            _clusterSize = clusterSize;
            _configure = configure;
            _clusterName = $"test-cluster-{Guid.NewGuid().ToString().Substring(0, 6)}";
        }

        protected virtual ClusterKind[] ClusterKinds => new []
        {
            new ClusterKind(EchoActor.Kind, EchoActor.Props.WithClusterRequestDeduplication()),
            new ClusterKind(EchoActor.Kind2, EchoActor.Props),
            new ClusterKind(EchoActor.LocalAffinityKind, EchoActor.Props).WithLocalAffinity()
            
        };

        public async Task InitializeAsync() => Members = await SpawnClusterNodes(_clusterSize, _configure).ConfigureAwait(false);

        public async Task DisposeAsync()
        {
            try
            {
                await Task.WhenAll(Members?.Select(cluster => cluster.ShutdownAsync()) ?? new[] {Task.CompletedTask}).ConfigureAwait(false);
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

        public IList<Cluster> Members { get; private set; }

        private async Task<IList<Cluster>> SpawnClusterNodes(
            int count,
            Func<ClusterConfig, ClusterConfig> configure = null
        ) => (await Task.WhenAll(
            Enumerable.Range(0, count)
                .Select(_ => SpawnClusterMember(configure))
        )).ToList();

        protected virtual async Task<Cluster> SpawnClusterMember(Func<ClusterConfig, ClusterConfig> configure)
        {
            var config = ClusterConfig.Setup(
                    _clusterName,
                    GetClusterProvider(),
                    GetIdentityLookup(_clusterName)
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
        public InMemoryClusterFixture() : base(3, config => config.WithActorRequestTimeout(TimeSpan.FromSeconds(4)))
        {
        }
    }
}