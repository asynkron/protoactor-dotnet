// -----------------------------------------------------------------------
// <copyright file="RequestAsyncStrategy.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public interface IClusterContext
    {
        Task<T?> RequestAsync<T>(ClusterIdentity clusterIdentity, object message, ISenderContext context, CancellationToken ct);
    }
}