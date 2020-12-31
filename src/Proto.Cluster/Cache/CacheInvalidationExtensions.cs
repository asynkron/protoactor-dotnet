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

        public static Props WithPidCacheInvalidation(this Props props)
            => props.WithContextDecorator(context => {
                    var cacheInvalidation = context.System.Extensions.Get<ClusterCacheInvalidation>();

                    if (cacheInvalidation is null)
                    {
                        Logger.LogWarning("ClusterCacheInvalidation extension is not registered");
                        return context;
                    }

                    return new CacheInvalidationContext(
                        context,
                        cacheInvalidation
                    );
                }
            );

        public static Cluster WithPidCacheInvalidation(this Cluster cluster)
        {
            _ = new ClusterCacheInvalidation(cluster);
            return cluster;
        }

        private class CacheInvalidationContext : ActorContextDecorator
        {
            private readonly ClusterCacheInvalidation _plugin;
            private Action<MessageEnvelope>? _callBack;

            public CacheInvalidationContext(IContext context, ClusterCacheInvalidation cacheInvalidation) : base(context)
                => _plugin = cacheInvalidation;

            public override async Task Receive(MessageEnvelope envelope)
            {
                await base.Receive(envelope);

                if (envelope.Message is ClusterInit init) _callBack = _plugin.ForActor(init.ClusterIdentity, Self!);
                else _callBack?.Invoke(envelope);
            }
        }
    }
}