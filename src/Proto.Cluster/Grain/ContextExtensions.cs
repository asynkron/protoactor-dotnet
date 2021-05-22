// -----------------------------------------------------------------------
// <copyright file="ContextExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public static class ContextExtensions
    {
        public static ClusterIdentity? ClusterIdentity(this IContext context) => context.Get<ClusterIdentity>();
    }
}