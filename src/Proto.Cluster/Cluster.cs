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

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster
    {
        private ClusterHeartBeat _clusterHeartBeat;

        public Cluster(ActorSystem system, ClusterConfig config)
        {
            Id = Guid.NewGuid();
            PidCache = new PidCache();
            System = system;
            Config = config;

            _clusterHeartBeat = new ClusterHeartBeat(this);
            system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    foreach (var member in e.Left) PidCache.RemoveByMember(member);
                }
            );
        }

        public ILogger Logger { get; private set; } = null!;
        public IRequestAsyncStrategy AsyncStrategy { get; private set; } = null!;

        public Guid Id { get; }

        public ClusterConfig Config { get; private set; }

        public ActorSystem System { get; }

        public Remote.Remote Remote { get; private set; } = null!;

        public MemberList MemberList { get; private set; } = null!;

        internal IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Address;

        public PidCache PidCache { get; } 

        public async Task StartMemberAsync()
        {
            await BeginStartAsync(Config, false);
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

            Logger.LogInformation("Started as cluster member");
        }

        public async Task StartClientAsync()
        {
            await BeginStartAsync(Config, true);

            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;

            await Provider.StartClientAsync(
                this,
                Config.Name,
                host,
                port,
                MemberList
            );

            Logger.LogInformation("Started as cluster client");
        }

        private async Task BeginStartAsync(ClusterConfig config, bool client)
        {
            Config = config;
            Config.RemoteConfig.WithProtoMessages(ProtosReflection.Descriptor);

            //default to partition identity lookup
            IdentityLookup = config.IdentityLookup ?? new PartitionIdentityLookup();
            Remote = new Remote.Remote(System, Config.RemoteConfig);
            await Remote.StartAsync();
            Logger = Log.CreateLogger($"Cluster-{LoggerId}");
            Logger.LogInformation("Starting");
            MemberList = new MemberList(this);
            AsyncStrategy = new RequestAsyncStrategy(IdentityLookup, PidCache, System.Root, Logger);

            var kinds = Remote.GetKnownKinds();
            await IdentityLookup.SetupAsync(this, kinds, client);
            await _clusterHeartBeat.StartAsync();
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            await _clusterHeartBeat.ShutdownAsync();
            Logger.LogInformation("Stopping");
            if (graceful) await IdentityLookup!.ShutdownAsync();

            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);

            Logger.LogInformation("Stopped");
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
            return AsyncStrategy.RequestAsync<T>(identity, kind, message, ct);
        }
    }
}