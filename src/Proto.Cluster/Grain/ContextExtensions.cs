// -----------------------------------------------------------------------
// <copyright file="ContextExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;

namespace Proto.Cluster
{
    [PublicAPI]
    public static class ContextExtensions
    {
        /// <summary>
        /// When called on a plain actor context, ClusterIdentity will return null.
        /// On a virtual actor context ClusterIdentity is guaranteed to be populated when started.
        /// </summary>
        /// <param name="context">The actor context</param>
        /// <returns>The actor ClusterIdentity for virtual actors. Null when called from plain Actors</returns>
        public static ClusterIdentity? ClusterIdentity(this IContext context) => context.Get<ClusterIdentity>();
    }
}