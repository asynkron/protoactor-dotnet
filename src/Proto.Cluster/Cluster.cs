// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster
    {
        private static ILogger _logger = null!;

        private ClusterHeartBeat _clusterHeartBeat;

        private IRequestAsyncStrategy _requestAsyncStrategy = null!;

        public Cluster(ActorSystem system, Serialization serialization)
        {
            System = system;
            Remote = new Remote.Remote(system, serialization);
            _clusterHeartBeat = new ClusterHeartBeat(this);
            system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    foreach (var member in e.Left) PidCache.RemoveByMember(member);
                }
            );
        }

        public Guid Id { get; } = Guid.NewGuid();

        public ClusterConfig Config { get; private set; } = null!;

        public ActorSystem System { get; }

        public Remote.Remote Remote { get; }


        public MemberList MemberList { get; private set; } = null!;

        private IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Address;

        public PidCache PidCache { get; } = new PidCache();

        public Task StartMemberAsync(string clusterName, string address, int port, IClusterProvider cp)
        {
            return StartMemberAsync(new ClusterConfig(clusterName, address, port, cp));
        }

        public async Task StartMemberAsync(ClusterConfig config)
        {
            await BeginStartAsync(config, false);
            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;
            var kinds = Remote.GetKnownKinds();
            await Provider.StartMemberAsync(
                this,
                Config.Name,
                host,
                port,
                kinds,
                MemberList
            );

            _logger.LogInformation("Started as cluster member");
        }

        public async Task StartClientAsync(ClusterConfig config)
        {
            await BeginStartAsync(config, true);

            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;

            await Provider.StartClientAsync(
                this,
                Config.Name,
                host,
                port,
                MemberList
            );

            _logger.LogInformation("Started as cluster client");
        }

        private async Task BeginStartAsync(ClusterConfig config, bool client)
        {
            Config = config;

            //default to partition identity lookup
            IdentityLookup = config.IdentityLookup ?? new PartitionIdentityLookup();
            Remote.Start(Config.Address, Config.Port, Config.RemoteConfig);
            Remote.Serialization.RegisterFileDescriptor(ProtosReflection.Descriptor);
            _logger = Log.CreateLogger($"Cluster-{LoggerId}");
            _logger.LogInformation("Starting");
            MemberList = new MemberList(this);
            _requestAsyncStrategy = new RequestAsyncStrategy(IdentityLookup, PidCache, System.Root, _logger);

            var kinds = Remote.GetKnownKinds();
            await IdentityLookup.SetupAsync(this, kinds, client);
            await _clusterHeartBeat.StartAsync();
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            await _clusterHeartBeat.ShutdownAsync();
            _logger.LogInformation("Stopping");
            if (graceful) await IdentityLookup!.ShutdownAsync();

            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);

            _logger.LogInformation("Stopped");
        }

        public Task<PID?> GetAsync(string identity, string kind)
        {
            return GetAsync(identity, kind, CancellationToken.None);
        }

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct)
        {
            return IdentityLookup!.GetAsync(identity, kind, ct);
        }

        public Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct)
        {
            return _requestAsyncStrategy.RequestAsync<T>(identity, kind, message, ct);
        }
    }
}