// -----------------------------------------------------------------------
// <copyright file="CacheInvalidationExtensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Cache
{
    public static class CacheInvalidationExtensions
    {
        private static readonly ILogger Logger = Log.CreateLogger(nameof(CacheInvalidationExtensions));

        /// <summary>
        /// Enable PidCache invalidation for ClusterKind. Requires PidCacheInvalidation to be enabled on the Cluster.
        /// </summary>
        /// <param name="clusterKind"></param>
        /// <returns></returns>
        public static ClusterKind WithPidCacheInvalidation(this ClusterKind clusterKind)
            => clusterKind with {Props = clusterKind.Props.WithPidCacheInvalidation()};

        /// <summary>
        /// Enable PidCache invalidation for the Cluster.
        /// It also needs to be enabled for each ClusterKind which needs cache invalidation individually 
        /// </summary>
        /// <param name="cluster"></param>
        /// <returns></returns>
        public static Cluster WithPidCacheInvalidation(this Cluster cluster)
        {
            _ = new ClusterCacheInvalidation(cluster);
            return cluster;
        }

        private static Props WithPidCacheInvalidation(this Props props)
            => props.WithReceiverMiddleware(receiver => {
                    return (context, envelope) => {
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
}