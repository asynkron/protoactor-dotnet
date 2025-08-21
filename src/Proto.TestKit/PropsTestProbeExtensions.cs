using System.Threading.Tasks;

namespace Proto.TestKit;

/// <summary>
/// Extension methods to attach <see cref="TestProbe"/> instances to actor pipelines.
/// </summary>
public static class PropsTestProbeExtensions
{
    /// <summary>
    /// Captures messages received by the actor after they are processed.
    /// </summary>
    public static Props WithTestReceiveProbe(this Props props, TestProbe probe) =>
        props.WithReceiverMiddleware(next => async (ctx, env) =>
        {
            await next(ctx, env);
            probe.Context.Send(probe.Context.Self, new MessageEnvelope(env.Message, env.Sender));
        });

    /// <summary>
    /// Captures messages sent by the actor before they are delivered to the target.
    /// </summary>
    public static Props WithTestSendProbe(this Props props, TestProbe probe) =>
        props.WithSenderMiddleware(next => async (ctx, target, env) =>
        {
            probe.Context.Send(probe.Context.Self, new MessageEnvelope(env.Message, target));
            await next(ctx, target, env);
        });
}
