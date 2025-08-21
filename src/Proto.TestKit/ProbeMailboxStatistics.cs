using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// IMailboxStatistics implementation that forwards processed messages to a <see cref="TestProbe"/>.
/// </summary>
public class ProbeMailboxStatistics : IMailboxStatistics
{
    private readonly TestProbe _probe;

    public ProbeMailboxStatistics(TestProbe probe) => _probe = probe;

    public void MailboxStarted()
    {
    }

    public void MessagePosted(object message)
    {
    }

    public void MessageReceived(object message) =>
        _probe.Context.Send(_probe.Context.Self, message);

    public void MailboxEmpty()
    {
    }
}
