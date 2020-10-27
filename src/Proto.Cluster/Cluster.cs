// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Linq;
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
            Config.RemoteConfig.WithProtoMessages(ProtosReflection.Descriptor);

            _clusterHeartBeat = new ClusterHeartBeat(this);
            system.EventStream.Subscribe<ClusterTopology>(e =>
                {
                    foreach (var member in e.Left) PidCache.RemoveByMember(member);
                }
            );
        }

        public ILogger Logger { get; private set; } = null!;
        public IClusterContext ClusterContext { get; private set; } = null!;

        public Guid Id { get; }

        public ClusterConfig Config { get; }

        public ActorSystem System { get; }

        public Remote.Remote Remote { get; private set; } = null!;

        public MemberList MemberList { get; private set; } = null!;

        internal IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Address;

        public PidCache PidCache { get; }

        public string[] GetClusterKinds() => Config.ClusterKinds.Keys.ToArray();

        public async Task StartMemberAsync()
        {
            await BeginStartAsync(false);
            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;
            var kinds = GetClusterKinds();
            await Provider.StartMemberAsync(
                this,
                Config.ClusterName,
                host,
                port,
                kinds,
                MemberList
            );

            if (Config.EnableDeadLetterResponse) StartDeadLetterResponses();

            Logger.LogInformation("Started as cluster member");
        }

        public async Task StartClientAsync()
        {
            await BeginStartAsync(true);

            var (host, port) = System.GetAddress();

            Provider = Config.ClusterProvider;

            await Provider.StartClientAsync(
                this,
                Config.ClusterName,
                host,
                port,
                MemberList
            );

            Logger.LogInformation("Started as cluster client");
        }

        private async Task BeginStartAsync( bool client)
        {
            //default to partition identity lookup
            IdentityLookup = Config.IdentityLookup ?? new PartitionIdentityLookup();
            Remote = new Remote.Remote(System, Config.RemoteConfig);
            await Remote.StartAsync();
            Logger = Log.CreateLogger($"Cluster-{LoggerId}");
            Logger.LogInformation("Starting");
            MemberList = new MemberList(this);
            ClusterContext = new DefaultClusterContext(IdentityLookup, PidCache, System.Root, Logger);

            var kinds = GetClusterKinds();
            await IdentityLookup.SetupAsync(this, kinds, client);
            await _clusterHeartBeat.StartAsync();
        }

        private void StartDeadLetterResponses()
        {
            System.EventStream.Subscribe<DeadLetterEvent>(@event =>
            {
                if (@event.Sender == null) return;
                if (System.Address == @event.Sender.Address)
                {
                    Logger?.LogInformation("Sending dead letter locally");
                    System.Root.Send(@event.Sender, DeadLetterResponse.Instance);
                }
                else
                {
                    Logger?.LogInformation("Sending dead letter to remote");
                    Remote.SendMessage(@event.Sender, DeadLetterResponse.Instance, -1);
                }
            });
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

        public Task<PID?> GetAsync(string identity, string kind) => GetAsync(identity, kind, CancellationToken.None);

        public Task<PID?> GetAsync(string identity, string kind, CancellationToken ct) => IdentityLookup!.GetAsync(identity, kind, ct);

        public Task<T> RequestAsync<T>(string identity, string kind, object message, CancellationToken ct) => ClusterContext.RequestAsync<T>(identity, kind, message, ct);

        public Props GetClusterKind(string kind)
        {
            if (!Config.ClusterKinds.TryGetValue(kind, out var props))
            {
                throw new ArgumentException($"No Props found for kind '{kind}'");
            }

            return props;
        }
    }
}