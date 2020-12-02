// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster
{
    public static class Extensions
    {
        public static ActorSystem WithCluster(this ActorSystem system, ClusterConfig config)
        {
            _ = new Cluster(system, config);
            return system;
        }

        public static Cluster Cluster(this ActorSystem system) => system.Extensions.Get<Cluster>();

        public static Cluster Cluster(this IContext context) => context.System.Extensions.Get<Cluster>();
    }
}