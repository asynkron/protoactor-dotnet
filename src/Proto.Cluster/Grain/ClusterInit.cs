// -----------------------------------------------------------------------
// <copyright file="ClusterInit.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster
{
    public class ClusterInit
    {
        public ClusterInit(ClusterIdentity clusterIdentity, Cluster cluster)
        {
            ClusterIdentity = clusterIdentity;
            Cluster = cluster;
        }

        public ClusterIdentity ClusterIdentity { get; }

        public string Identity => ClusterIdentity.Identity;
        public string Kind => ClusterIdentity.Kind;

        public Cluster Cluster { get; }
    }
}