using System;
using System.Collections.Generic;
using System.Threading;
using Proto.Mailbox;

namespace Proto.TestFixtures;

public class TestMailboxStatistics : IMailboxStatistics
{
    private readonly Func<object, bool> _waitForReceived;

    public TestMailboxStatistics()
    {
    }

    public TestMailboxStatistics(Func<object, bool> waitForReceived)
    {
        _waitForReceived = waitForReceived;
    }

    public ManualResetEventSlim Reset { get; } = new();
    public List<object> Stats { get; } = new();
    public List<object> Posted { get; } = new();
    public List<object> Received { get; } = new();

    public void MailboxStarted() => Stats.Add("Started");

    public void MessagePosted(object message)
    {
        Stats.Add(message);
        Posted.Add(message);
    }

    public void MessageReceived(object message)
    {
        Stats.Add(message);
        Received.Add(message);

        if (_waitForReceived is not null && _waitForReceived(message))
        {
            Reset.Set();
        }
    }

    public void MailboxEmpty() => Stats.Add("Empty");
}