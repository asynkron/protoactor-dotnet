// -----------------------------------------------------------------------
// <copyright file="ClusterExtension.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using Proto.Cluster.Metrics;

namespace Proto.Cluster
{
    public static class Extensions
    {
        public static ActorSystem WithCluster(this ActorSystem system, ClusterConfig config)
        {
            _ = new Cluster(system, config);
            return system;
        }

        public static Cluster Cluster(this ActorSystem system)
            => system.Extensions.Get<Cluster>() ?? throw new NotSupportedException("Cluster has not been configured");

        public static Cluster Cluster(this IContext context)
            => context.System.Extensions.Get<Cluster>() ?? throw new NotSupportedException("Cluster has not been configured");

        public static Task<T> ClusterRequestAsync<T>(this IContext context, string identity, string kind, object message, CancellationToken ct)
        {
            var cluster = context.System.Extensions.Get<Cluster>();
            //call cluster RequestAsync using actor context
            return cluster.RequestAsync<T>(identity, kind, message, context, ct);
        }

        public static Props WithClusterInit(this Props props, Cluster cluster, ClusterIdentity clusterIdentity)
        {
            return props.WithReceiverMiddleware(
                baseReceive =>
                    (ctx, env) => {
                        return env.Message switch
                        {
                            Started => HandleStart(cluster, clusterIdentity, baseReceive, ctx, env),
                            Stopped => HandleStopped(cluster, clusterIdentity, baseReceive, ctx, env),
                            _       => baseReceive(ctx, env)
                        };
                    }
            );

            static async Task HandleStart(
                Cluster cluster,
                ClusterIdentity clusterIdentity,
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope startEnvelope
            )
            {
                await baseReceive(ctx, startEnvelope);
                var grainInit = new ClusterInit(clusterIdentity, cluster);
                var grainInitEnvelope = new MessageEnvelope(grainInit, null);
                cluster.System.Metrics.Get<ClusterMetrics>().ClusterActorCount.Inc( new []{cluster.System.Id,cluster.System.Address,  clusterIdentity.Kind});
                await baseReceive(ctx, grainInitEnvelope);
            }

            static async Task HandleStopped(
                Cluster cluster,
                ClusterIdentity clusterIdentity,
                Receiver baseReceive,
                IReceiverContext ctx,
                MessageEnvelope startEnvelope
            )
            {
                cluster.System.Metrics.Get<ClusterMetrics>().ClusterActorCount.Inc( new[]{cluster.System.Id,cluster.System.Address, clusterIdentity.Kind},-1);
                await baseReceive(ctx, startEnvelope);
            }
        }
    }
}