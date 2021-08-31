// -----------------------------------------------------------------------
// <copyright file="ClusterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Proto.Cluster.Identity;

namespace Proto.Cluster
{
    public enum ClusterStartMode
    {
        Member,
        Client,
    }
    
    [PublicAPI]
    public record ClusterConfig : IActorSystemConfig
    {
        private ClusterConfig(string clusterName, IClusterProvider clusterProvider, IIdentityLookup identityLookup)
        {
            ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
            ClusterProvider = clusterProvider ?? throw new ArgumentNullException(nameof(clusterProvider));
            TimeoutTimespan = TimeSpan.FromSeconds(5);
            ActorRequestTimeout = TimeSpan.FromSeconds(5);
            ActorSpawnTimeout = TimeSpan.FromSeconds(5);
            ActorActivationTimeout = TimeSpan.FromSeconds(5);
            MaxNumberOfEventsInRequestLogThrottlePeriod = 3;
            RequestLogThrottlePeriod = TimeSpan.FromSeconds(2);
            GossipInterval = TimeSpan.FromMilliseconds(300);
            GossipRequestTimeout = TimeSpan.FromMilliseconds(500);
            GossipFanout = 3;
            ClusterRequestDeDuplication = true;
            ClusterRequestDeDuplicationWindow = TimeSpan.FromSeconds(30);
            IdentityLookup = identityLookup;
            MemberStrategyBuilder = (_, _) => new SimpleMemberStrategy();
            PubSubBatchSize = 2000;
            StartMode = ClusterStartMode.Member;
        }

        public Func<Cluster, string, IMemberStrategy> MemberStrategyBuilder { get; init; }
        
        public ClusterStartMode StartMode { get; init; }
        
        public string ClusterName { get; }

        public ImmutableList<ClusterKind> ClusterKinds { get; init; } = ImmutableList<ClusterKind>.Empty;

        public IClusterProvider ClusterProvider { get; }

        public int PubSubBatchSize { get; init; }
        public TimeSpan TimeoutTimespan { get; init; }
        public TimeSpan ActorRequestTimeout { get; init; }
        public TimeSpan ActorSpawnTimeout { get; init; }
        public TimeSpan ActorActivationTimeout { get; init; }
        public TimeSpan RequestLogThrottlePeriod { get; init; }
        public int MaxNumberOfEventsInRequestLogThrottlePeriod { get; init; }

        public int GossipFanout { get; init;  }

        public IIdentityLookup IdentityLookup { get; }
        public TimeSpan GossipInterval { get; init; }
        public TimeSpan GossipRequestTimeout { get; init; }

        public bool ClusterRequestDeDuplication { get; init; }

        public TimeSpan ClusterRequestDeDuplicationWindow { get; init; }

        public ClusterConfig WithStartMode(ClusterStartMode startMode) =>
            this with {StartMode = startMode};

        public Func<Cluster, IClusterContext> ClusterContextProducer { get; init; } =
            c => new DefaultClusterContext(c.IdentityLookup, c.PidCache, c.Config.ToClusterContextConfig(),c.System.Shutdown);

        public ClusterConfig WithTimeout(TimeSpan timeSpan) =>
            this with {TimeoutTimespan = timeSpan};

        public ClusterConfig WithActorRequestTimeout(TimeSpan timeSpan) =>
            this with {ActorRequestTimeout = timeSpan};
        
        public ClusterConfig WithActorSpawnTimeout(TimeSpan timeSpan) =>
            this with {ActorSpawnTimeout = timeSpan};
        
        public ClusterConfig WithActorActivationTimeout(TimeSpan timeSpan) =>
            this with {ActorActivationTimeout = timeSpan};

        public ClusterConfig WithRequestLogThrottlePeriod(TimeSpan timeSpan) =>
            this with {RequestLogThrottlePeriod = timeSpan};

        public ClusterConfig WithPubSubBatchSize(int batchSize) =>
            this with {PubSubBatchSize = batchSize};

        public ClusterConfig WithMaxNumberOfEventsInRequestLogThrottlePeriod(int max) =>
            this with {MaxNumberOfEventsInRequestLogThrottlePeriod = max};

        public ClusterConfig WithClusterKind(string kind, Props prop)
            => WithClusterKind(new ClusterKind(kind, prop));

        public ClusterConfig WithClusterKind(string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder) =>
            WithClusterKind(new ClusterKind(kind, prop) {StrategyBuilder = strategyBuilder});

        public ClusterConfig WithClusterKinds(params (string kind, Props prop)[] knownKinds) =>
            WithClusterKinds(knownKinds.Select(k => new ClusterKind(k.kind, k.prop)).ToArray());

        public ClusterConfig WithClusterKinds(params (string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder)[] knownKinds) =>
            WithClusterKinds(knownKinds.Select(k => new ClusterKind(k.kind, k.prop) {StrategyBuilder = k.strategyBuilder}).ToArray());

        public ClusterConfig WithClusterKind(ClusterKind clusterKind) => WithClusterKinds(clusterKind);

        public ClusterConfig WithClusterKinds(params ClusterKind[] clusterKinds)
            => this with {ClusterKinds = ClusterKinds.AddRange(clusterKinds)};
        
        public ClusterConfig WithMemberStrategyBuilder(Func<Cluster, string, IMemberStrategy> builder) =>
            this with {MemberStrategyBuilder = builder};

        public ClusterConfig WithClusterContextProducer(Func<Cluster, IClusterContext> producer) =>
            this with {ClusterContextProducer = producer};
        
        public ClusterConfig WithGossipInterval(TimeSpan interval) =>
            this with {GossipInterval = interval};
        
        public ClusterConfig WithGossipFanOut(int fanout) =>
            this with {GossipFanout = fanout};

        public ClusterConfig WithGossipRequestTimeout(TimeSpan timeout) =>
            this with { GossipRequestTimeout = timeout };


        public static ClusterConfig Setup(
            string clusterName,
            IClusterProvider clusterProvider,
            IIdentityLookup identityLookup
        ) =>
            new(clusterName, clusterProvider, identityLookup);

        async Task IActorSystemConfig.Apply(ActorSystem system)
        {
            var cluster = system.WithCluster(this).Cluster();

            await cluster.StartAsync();
            
        }
    }
}