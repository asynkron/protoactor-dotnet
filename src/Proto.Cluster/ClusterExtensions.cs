// -----------------------------------------------------------------------
// <copyright file="ClusterExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class ClusterExtensions
    {
        public static Task<PID?> GetAsync(this Cluster cluster, string identity, string kind, CancellationToken ct) =>
            cluster.GetAsync(new ClusterIdentity {Identity = identity, Kind = kind}, ct);
        public static Task<T> RequestAsync<T>(this Cluster cluster, string identity, string kind, object message, CancellationToken ct)
            => cluster.RequestAsync<T>(new ClusterIdentity {Identity = identity, Kind = kind}, message, cluster.System.Root, ct);

        public static Task<T> RequestAsync<T>(this Cluster cluster, string identity, string kind, object message, ISenderContext context, CancellationToken ct) =>
            cluster.RequestAsync<T>(new ClusterIdentity {Identity = identity, Kind = kind}, message, context, ct)!;
        
        public static Task<T> RequestAsync<T>(this Cluster cluster, ClusterIdentity clusterIdentity, object message, CancellationToken ct) =>
            cluster.RequestAsync<T>(clusterIdentity, message, cluster.System.Root, ct)!;
    }
}