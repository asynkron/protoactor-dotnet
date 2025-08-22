using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// Extension methods to attach <see cref="TestMailboxStats"/> to actor props.
/// </summary>
public static class PropsTestMailboxStatsExtensions
{
    /// <summary>
    /// Configures the actor to use an <see cref="UnboundedMailbox"/> instrumented with the provided <paramref name="stats"/>.
    /// </summary>
    public static Props WithTestMailboxStats(this Props props, TestMailboxStats stats) =>
        props.WithMailbox(() => UnboundedMailbox.Create(stats));
}
