// -----------------------------------------------------------------------
// <copyright file="Cluster.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.IdentityLookup;
using Proto.Cluster.Partition;
using Proto.Extensions;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster : IActorSystemExtension<Cluster>
    {
        private ClusterHeartBeat _clusterHeartBeat;

        public Cluster(ActorSystem system, ClusterConfig config)
        {
            system.Extensions.Register(this);

            Id = Guid.NewGuid();
            PidCache = new PidCache();
            System = system;
            Config = config;
            system.Serialization().RegisterFileDescriptor(ProtosReflection.Descriptor);

            _clusterHeartBeat = new ClusterHeartBeat(this);
            system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    foreach (var member in e.Left)
                    {
                        PidCache.RemoveByMember(member);
                    }
                }
            );
        }

        public ILogger Logger { get; private set; } = null!;
        public IClusterContext ClusterContext { get; private set; } = null!;

        public Guid Id { get; }

        public ClusterConfig Config { get; }

        public ActorSystem System { get; }

        public IRemote Remote { get; private set; } = null!;

        public MemberList MemberList { get; private set; } = null!;

        internal IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Address;

        public PidCache PidCache { get; }

        public string[] GetClusterKinds() => Config.ClusterKinds.Keys.ToArray();

        public async Task StartMemberAsync()
        {
            await BeginStartAsync(false);
            Provider = Config.ClusterProvider;
            var kinds = GetClusterKinds();
            await Provider.StartMemberAsync(this);

            Logger.LogInformation("Started as cluster member");
        }

        public async Task StartClientAsync()
        {
            await BeginStartAsync(true);
            Provider = Config.ClusterProvider;

            await Provider.StartClientAsync(this);

            Logger.LogInformation("Started as cluster client");
        }

        private async Task BeginStartAsync(bool client)
        {
            //default to partition identity lookup
            IdentityLookup = Config.IdentityLookup ?? new PartitionIdentityLookup();
            Remote = System.Extensions.Get<IRemote>();
            await Remote.StartAsync();
            Logger = Log.CreateLogger($"Cluster-{LoggerId}");
            Logger.LogInformation("Starting");
            MemberList = new MemberList(this);
            ClusterContext = new DefaultClusterContext(IdentityLookup, PidCache, System.Root, Logger);

            var kinds = GetClusterKinds();
            await IdentityLookup.SetupAsync(this, kinds, client);
            await _clusterHeartBeat.StartAsync();
        }

        public async Task ShutdownAsync(bool graceful = true)
        {
            Logger.LogInformation("Stopping Cluster {Id}", Id);

            await _clusterHeartBeat.ShutdownAsync();
            if (graceful) await IdentityLookup!.ShutdownAsync();
            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);

            Logger.LogInformation("Stopped Cluster {Id}", Id);
        }

        public Task<PID?> GetAsync(string identity, string kind) => GetAsync(identity, kind, CancellationToken.None);

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct) =>
            IdentityLookup!.GetAsync(new ClusterIdentity {Identity = identity, Kind = kind}, ct);

        public Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct) =>
            ClusterContext.RequestAsync<T>(new ClusterIdentity {Identity = identity, Kind = kind}, message, ct);
        
        public Task<T> RequestAsync<T>(string identity, string kind, object message, ISenderContext context ,CancellationToken ct) =>
            ClusterContext.RequestAsync<T>(new ClusterIdentity {Identity = identity, Kind = kind}, message, context, ct);

        public Props GetClusterKind(string kind)
        {
            if (!Config.ClusterKinds.TryGetValue(kind, out var props))
                throw new ArgumentException($"No Props found for kind '{kind}'");

            return props;
        }
    }
}