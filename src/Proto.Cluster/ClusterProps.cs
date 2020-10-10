using System.Threading.Tasks;

namespace Proto.Cluster
{
    public static class ClusterProps
    {
        public static Props WithClusterInit(this Props props, Cluster cluster, string identity, string kind)
        {
            return props.WithReceiverMiddleware(baseReceive =>
                (ctx, env) =>
                    env.Message is Started
                        ? HandleStart(cluster, identity, kind, baseReceive, ctx, env)
                        : baseReceive(ctx, env)
            );
        }

        private static async Task HandleStart(Cluster cluster, string identity, string kind, Receiver baseReceive,
            IReceiverContext ctx, MessageEnvelope env)
        {
            await baseReceive(ctx, env);
            var grainInit = new GrainInit(identity, kind, cluster);
            var grainInitEnvelope = new MessageEnvelope(grainInit, null);
            await baseReceive(ctx, grainInitEnvelope);
        }
    }
}