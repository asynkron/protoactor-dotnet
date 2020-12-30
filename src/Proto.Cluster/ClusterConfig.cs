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
            HeartBeatInterval = TimeSpan.FromSeconds(30);
            MemberStrategyBuilder = (_, _) => new SimpleMemberStrategy();
            ClusterKinds = ImmutableDictionary<string, Props>.Empty;
            IdentityLookup = identityLookup;
        }

        public string ClusterName { get; }

        public ImmutableDictionary<string, Props> ClusterKinds { get; init; }

        public IClusterProvider ClusterProvider { get; }

        public TimeSpan TimeoutTimespan { get; init; }

        public Func<Cluster, string, IMemberStrategy> MemberStrategyBuilder { get; init; }

        public IIdentityLookup? IdentityLookup { get; }
        public TimeSpan HeartBeatInterval { get; init; }

        public Func<Cluster, IClusterContext> ClusterContextProducer { get; init; } =
            c => new DefaultClusterContext(c.IdentityLookup, c.PidCache, c.Logger);

        public ClusterConfig WithTimeout(TimeSpan timeSpan) =>
            this with {TimeoutTimespan = timeSpan};

        public ClusterConfig WithMemberStrategyBuilder(Func<Cluster, string, IMemberStrategy> builder) =>
            this with {MemberStrategyBuilder = builder};

        public ClusterConfig WithClusterKind(string kind, Props prop) =>
            this with {ClusterKinds = ClusterKinds.Add(kind, prop)};

        public ClusterConfig WithClusterKinds(params (string kind, Props prop)[] knownKinds) =>
            this with
            {
                ClusterKinds = ClusterKinds
                    .AddRange(knownKinds
                        .Select(kk => new KeyValuePair<string, Props>(kk.kind, kk.prop))
                    )
            };

        public static ClusterConfig Setup(
            string clusterName,
            IClusterProvider clusterProvider,
            IIdentityLookup identityLookup
        ) =>
            new(clusterName, clusterProvider, identityLookup);
    }
}