// -----------------------------------------------------------------------
// <copyright file="ClusterProps.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class ClusterProps
    {
        public static Props WithClusterInit(this Props props, Cluster cluster, ClusterIdentity clusterIdentity)
        {
            return props.WithReceiverMiddleware(baseReceive =>
                (ctx, env) =>
                    env.Message is Started
                        ? HandleStart(cluster, clusterIdentity, baseReceive, ctx, env)
                        : baseReceive(ctx, env)
            );
        }

        private static async Task HandleStart(Cluster cluster, ClusterIdentity clusterIdentity, Receiver baseReceive,
            IReceiverContext ctx, MessageEnvelope startEnvelope)
        {
            await baseReceive(ctx, startEnvelope);
            var grainInit = new ClusterInit(clusterIdentity, cluster);
            var grainInitEnvelope = new MessageEnvelope(grainInit, null);
            await baseReceive(ctx, grainInitEnvelope);
        }
    }
}