// -----------------------------------------------------------------------
// <copyright file="ClusterPlugin.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;
using Proto.Extensions;
using Proto.Remote;

namespace Proto.Cluster
{
    public class ClusterExtension : StartableActorSystemExtension<ClusterExtension>
    {
        public Cluster Cluster { get; }
        

        public ClusterExtension(ActorSystem system, Cluster cluster) :base(system)
        {
            AddDependency<RemoteExtension>();
            Cluster = cluster;
        }
    }
}