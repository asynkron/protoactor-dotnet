// -----------------------------------------------------------------------
// <copyright file="ClusterConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using JetBrains.Annotations;
using Proto.Cluster.Identity;

namespace Proto.Cluster
{
    [PublicAPI]
    public record ClusterConfig
    {
        private ClusterConfig(string clusterName, IClusterProvider clusterProvider, IIdentityLookup identityLookup)
        {
            ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
            ClusterProvider = clusterProvider ?? throw new ArgumentNullException(nameof(clusterProvider));
            TimeoutTimespan = TimeSpan.FromSeconds(5);
            ActorRequestTimeout = TimeSpan.FromSeconds(5);
            MaxNumberOfEventsInRequestLogThrottlePeriod = 3;
            RequestLogThrottlePeriod = TimeSpan.FromSeconds(2);
            HeartBeatInterval = TimeSpan.FromSeconds(30);
            ClusterRequestDeDuplication = true;
            ClusterRequestDeDuplicationWindow = TimeSpan.FromSeconds(30);
            IdentityLookup = identityLookup;
        }

        public string ClusterName { get; }

        public ImmutableList<ClusterKind> ClusterKinds { get; init; } = ImmutableList<ClusterKind>.Empty;

        public IClusterProvider ClusterProvider { get; }

        public TimeSpan TimeoutTimespan { get; init; }
        public TimeSpan ActorRequestTimeout { get; init; }
        public TimeSpan RequestLogThrottlePeriod { get; init; }
        public int MaxNumberOfEventsInRequestLogThrottlePeriod { get; init; }

        public IIdentityLookup? IdentityLookup { get; }
        public TimeSpan HeartBeatInterval { get; init; }

        public bool ClusterRequestDeDuplication { get; init; }

        public TimeSpan ClusterRequestDeDuplicationWindow { get; init; }

        public Func<Cluster, IClusterContext> ClusterContextProducer { get; init; } =
            c => new DefaultClusterContext(c.IdentityLookup, c.PidCache, c.Logger, c.Config.ToClusterContextConfig());

        public ClusterConfig WithTimeout(TimeSpan timeSpan) =>
            this with {TimeoutTimespan = timeSpan};

        public ClusterConfig WithActorRequestTimeout(TimeSpan timeSpan) =>
            this with {ActorRequestTimeout = timeSpan};

        public ClusterConfig WithRequestLogThrottlePeriod(TimeSpan timeSpan) =>
            this with {RequestLogThrottlePeriod = timeSpan};

        public ClusterConfig WithMaxNumberOfEventsInRequestLogThrottlePeriod(int max) =>
            this with {MaxNumberOfEventsInRequestLogThrottlePeriod = max};

        public ClusterConfig WithClusterKind(string kind, Props prop) =>
            this with {ClusterKinds = ClusterKinds.Add(new ClusterKind(kind,prop, _ => new SimpleMemberStrategy()))};
        
        public ClusterConfig WithClusterKind(string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder) =>
            this with {ClusterKinds = ClusterKinds.Add(new ClusterKind(kind,prop,strategyBuilder))};

        public ClusterConfig WithClusterKinds(params (string kind, Props prop)[] knownKinds) =>
            this with
            {
                ClusterKinds = ClusterKinds.AddRange(knownKinds.Select(k => new ClusterKind(k.kind,k.prop, _ => new SimpleMemberStrategy())).ToArray())
            };
        public ClusterConfig WithClusterKinds(params (string kind, Props prop, Func<Cluster, IMemberStrategy> strategyBuilder)[] knownKinds) =>
            this with
            {
                ClusterKinds = ClusterKinds.AddRange(knownKinds.Select(k => new ClusterKind(k.kind,k.prop,k.strategyBuilder)).ToArray())
            };

        public static ClusterConfig Setup(
            string clusterName,
            IClusterProvider clusterProvider,
            IIdentityLookup identityLookup
        ) =>
            new(clusterName, clusterProvider, identityLookup);
    }
}