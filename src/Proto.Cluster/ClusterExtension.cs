// -----------------------------------------------------------------------
// <copyright file="ClusterPlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Extensions;
using Proto.Remote;

namespace Proto.Cluster
{
    public class ClusterExtension : ActorSystemExtension<ClusterExtension>
    {
        public Cluster Cluster { get; }

        public ClusterExtension(Cluster cluster)
        {
            AddDependency<RemoteExtension>();
            Cluster = cluster;
        }
    }
}