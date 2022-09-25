// -----------------------------------------------------------------------
// <copyright file="CacheInvalidationExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Cache;

public static class CacheInvalidationExtensions
{
    private static readonly ILogger Logger = Log.CreateLogger(nameof(CacheInvalidationExtensions));

    /// <summary>
    ///     Enable PidCache invalidation for ClusterKind. If invalidation is enabled, other members in the cluster will learn
    ///     about
    ///     the virtual actor deactivation on this cluster and will clear their <see cref="PidCache" />. If not enabled,
    ///     other members will find out about stale entry in the PidCache when doing next request to this virtual actor.
    ///     <br />
    ///     Requires PidCacheInvalidation to be enabled on the Cluster. See <see cref="WithPidCacheInvalidation(Cluster)" />
    /// </summary>
    /// <param name="clusterKind"></param>
    /// <returns></returns>
    public static ClusterKind WithPidCacheInvalidation(this ClusterKind clusterKind) =>
        clusterKind with { Props = clusterKind.Props.WithPidCacheInvalidation() };

    /// <summary>
    ///     Enable PidCache invalidation for the Cluster. If invalidation is enabled, other members in the cluster will learn
    ///     about
    ///     the virtual actor deactivation on this cluster and will clear their <see cref="PidCache" />. If not enabled,
    ///     other members will find out about stale entry in the PidCache when doing next request to this virtual actor.
    ///     <br />
    ///     Invalidation also needs to be enabled for each ClusterKind which needs it individually. See
    ///     <see cref="WithPidCacheInvalidation(ClusterKind)" />
    /// </summary>
    /// <param name="cluster"></param>
    /// <returns></returns>
    public static Cluster WithPidCacheInvalidation(this Cluster cluster)
    {
        _ = new ClusterCacheInvalidation(cluster);

        return cluster;
    }

    private static Props WithPidCacheInvalidation(this Props props) =>
        props.WithReceiverMiddleware(receiver =>
            {
                return (context, envelope) =>
                {
                    var task = receiver(context, envelope);

                    if (envelope.Message is Started)
                    {
                        Initialize(context);
                    }
                    else
                    {
                        context.Get<PidCacheInvalidator>()?.Invoke(envelope);
                    }

                    return task;
                };
            }
        );

    private static void Initialize(IInfoContext context)
    {
        var plugin = context.System.Extensions.Get<ClusterCacheInvalidation>();

        if (plugin is not null)
        {
            context.Set(plugin.GetInvalidator(context.Get<ClusterIdentity>()!, context.Self));
        }
        else
        {
            Logger.LogWarning("PidCacheInvalidation is not enabled on the cluster");
        }
    }
}