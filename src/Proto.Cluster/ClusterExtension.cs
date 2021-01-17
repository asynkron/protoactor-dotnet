// -----------------------------------------------------------------------
// <copyright file="ClusterPlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Extensions;

namespace Proto.Cluster
{
    public class ClusterExtension : IActorSystemExtension<ClusterExtension>
    {
        public Cluster Cluster { get; }

        public ClusterExtension(Cluster cluster)
        {
            Cluster = cluster;
        }
    }
}