using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// Extension methods to attach <see cref="TestProbe"/> instances to actor pipelines.
/// </summary>
public static class PropsTestProbeExtensions
{
    /// <summary>
    /// Captures messages received by the actor after they are processed.
    /// </summary>
    public static Props WithReceiveProbe(this Props props, TestProbe probe) =>
        props.WithReceiverMiddleware(next => async (ctx, env) =>
        {
            await next(ctx, env);
            probe.Context.Send(probe.Context.Self, new MessageEnvelope(env.Message, env.Sender));
        });

    /// <summary>
    /// Captures messages sent by the actor before they are delivered to the target.
    /// </summary>
    public static Props WithSendProbe(this Props props, TestProbe probe) =>
        props.WithSenderMiddleware(next => async (ctx, target, env) =>
        {
            probe.Context.Send(probe.Context.Self, new MessageEnvelope(env.Message, target));
            await next(ctx, target, env);
        });

    /// <summary>
    /// Captures messages processed by the mailbox using <see cref="ProbeMailboxStatistics"/>.
    /// Existing send and receive middleware can still be chained.
    /// </summary>
    public static Props WithMailboxProbe(this Props props, TestProbe probe) =>
        props.WithMailbox(() => UnboundedMailbox.Create(new ProbeMailboxStatistics(probe)));
}
