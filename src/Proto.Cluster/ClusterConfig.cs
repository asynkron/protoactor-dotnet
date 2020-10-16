// -----------------------------------------------------------------------
//   <copyright file="Cluster.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Proto.Cluster.IdentityLookup;
using Proto.Remote;

namespace Proto.Cluster
{
    [PublicAPI]
    public class ClusterConfig
    {
        private ClusterConfig(string clusterName, IClusterProvider clusterProvider, IIdentityLookup identityLookup,RemoteConfig remoteConfig)
        {
            ClusterName = clusterName ?? throw new ArgumentNullException(nameof(clusterName));
            ClusterProvider = clusterProvider ?? throw new ArgumentNullException(nameof(clusterProvider));
            RemoteConfig = remoteConfig ?? throw new ArgumentNullException(nameof(remoteConfig));
            TimeoutTimespan = TimeSpan.FromSeconds(5);
            HeartBeatInterval = TimeSpan.FromSeconds(30);
            MemberStrategyBuilder = kind => new SimpleMemberStrategy();
            ClusterKinds = new Dictionary<string, Props>();
        }

        public string ClusterName { get; }
        
        public Dictionary<string, Props> ClusterKinds { get; } 

        public IClusterProvider ClusterProvider { get; }

        public RemoteConfig RemoteConfig { get; }
        
        public TimeSpan TimeoutTimespan { get; private set; }

        public Func<string, IMemberStrategy> MemberStrategyBuilder { get; private set; }

        public IIdentityLookup? IdentityLookup { get; }
        public TimeSpan HeartBeatInterval { get; set; }

        public ClusterConfig WithTimeout(TimeSpan timeSpan)
        {
            TimeoutTimespan = timeSpan;
            return this;
        }

        public ClusterConfig WithMemberStrategyBuilder(Func<string, IMemberStrategy> builder)
        {
            MemberStrategyBuilder = builder;
            return this;
        }

        public ClusterConfig WithClusterKind(string kind, Props prop)
        {
            ClusterKinds.Add(kind, prop);
            return this;
        }

        public ClusterConfig WithClusterKinds(params (string kind, Props prop)[] knownKinds)
        {
            foreach (var (kind, prop) in knownKinds) ClusterKinds.Add(kind, prop);
            return this;
        }
        
        public static ClusterConfig Setup(string clusterName, IClusterProvider clusterProvider,
            IIdentityLookup identityLookup, RemoteConfig remoteConfig)
        {
            return new ClusterConfig(clusterName, clusterProvider, identityLookup, remoteConfig);
        }
    }
}