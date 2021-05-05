// -----------------------------------------------------------------------
// <copyright file="Cluster.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Identity;
using Proto.Cluster.Metrics;
using Proto.Cluster.PubSub;
using Proto.Extensions;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class Cluster : IActorSystemExtension<Cluster>
    {
        private Dictionary<string, ActivatedClusterKind> _clusterKinds = new();

        public Cluster(ActorSystem system, ClusterConfig config)
        {
            System = system;
            Config = config;

            system.Extensions.Register(this);
            system.Metrics.Register(new ClusterMetrics(system.Metrics));
            system.Serialization().RegisterFileDescriptor(ClusterContractsReflection.Descriptor);

            PidCache = new PidCache();
            ClusterHeartBeat = new ClusterHeartBeat(this);
            PubSub = new PubSubManager(this);

            SubscribeToTopologyEvents();
        }

        private ClusterHeartBeat ClusterHeartBeat { get; }

        public PubSubManager PubSub { get; }

        public static ILogger Logger { get; } = Log.CreateLogger<Cluster>();

        public IClusterContext ClusterContext { get; private set; } = null!;

        public ClusterConfig Config { get; }

        public ActorSystem System { get; }

        public IRemote Remote { get; private set; } = null!;

        public MemberList MemberList { get; private set; } = null!;

        internal IIdentityLookup IdentityLookup { get; set; } = null!;

        internal IClusterProvider Provider { get; set; } = null!;

        public string LoggerId => System.Id;

        public PidCache PidCache { get; }

        private void SubscribeToTopologyEvents() =>
            System.EventStream.Subscribe<ClusterTopology>(e => {
                    System.Metrics.Get<ClusterMetrics>().ClusterTopologyEventGauge.Set(e.Members.Count,
                        new[] {System.Id, System.Address, e.GetMembershipHashCode().ToString()}
                    );

                    foreach (var member in e.Left)
                    {
                        PidCache.RemoveByMember(member);
                    }
                }
            );

        public string[] GetClusterKinds() => _clusterKinds.Keys.ToArray();

        public async Task StartMemberAsync()
        {
            await BeginStartAsync(false);
            await Provider.StartMemberAsync(this);

            Logger.LogInformation("Started as cluster member");
        }

        public async Task StartClientAsync()
        {
            await BeginStartAsync(true);
            await Provider.StartClientAsync(this);

            Logger.LogInformation("Started as cluster client");
        }

        private async Task BeginStartAsync(bool client)
        {
            InitClusterKinds();
            Provider = Config.ClusterProvider;
            //default to partition identity lookup
            IdentityLookup = Config.IdentityLookup;

            Remote = System.Extensions.Get<IRemote>() ?? throw new NotSupportedException("Remote module must be configured when using cluster");

            await Remote.StartAsync();

            Logger.LogInformation("Starting");
            MemberList = new MemberList(this);
            ClusterContext = Config.ClusterContextProducer(this);

            var kinds = GetClusterKinds();
            await IdentityLookup.SetupAsync(this, kinds, client);
            InitIdentityProxy();
            await ClusterHeartBeat.StartAsync();
            await PubSub.StartAsync();
        }

        private void InitClusterKinds()
        {
            foreach (var clusterKind in Config.ClusterKinds)
            {
                _clusterKinds.Add(clusterKind.Name, clusterKind.Build(this));
            }
        }

        private void InitIdentityProxy()
            => System.Root.SpawnNamed(Props.FromProducer(() => new IdentityActivatorProxy(this)), IdentityActivatorProxy.ActorName);

        public async Task ShutdownAsync(bool graceful = true)
        {
            await System.ShutdownAsync();
            Logger.LogInformation("Stopping Cluster {Id}", System.Id);

            await ClusterHeartBeat.ShutdownAsync();
            if (graceful) await IdentityLookup!.ShutdownAsync();
            await Config!.ClusterProvider.ShutdownAsync(graceful);
            await Remote.ShutdownAsync(graceful);

            Logger.LogInformation("Stopped Cluster {Id}", System.Id);
        }

        public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct) => IdentityLookup!.GetAsync(clusterIdentity, ct);

        public Task<T> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct) =>
            ClusterContext.RequestAsync<T>(clusterIdentity, message, context, ct)!;

        public ActivatedClusterKind GetClusterKind(string kind)
        {
            if (!_clusterKinds.TryGetValue(kind, out var clusterKind))
                throw new ArgumentException($"No cluster kind '{kind}' was not found");

            return clusterKind;
        }

        public ActivatedClusterKind TryGetClusterKind(string kind)
        {
            _clusterKinds.TryGetValue(kind, out var clusterKind);

            return clusterKind;
        }

        public ClusterIdentity GetIdentity(string identity, string kind)
        {
            var id = new ClusterIdentity
            {
                Identity = identity,
                Kind = kind
            };

            if (PidCache.TryGet(id, out var pid))
            {
                id.CachedPid = pid;
            }

            return id;
        }
    }
}