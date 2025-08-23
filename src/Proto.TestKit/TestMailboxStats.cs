using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestKit;

/// <summary>
/// IMailboxStatistics implementation used to collect mailbox events in tests.
/// </summary>
public class TestMailboxStats : IMailboxStatistics
{
    private readonly Func<object, bool>? _waitForReceived;

    public TestMailboxStats()
    {
    }

    public TestMailboxStats(Func<object, bool> waitForReceived)
    {
        _waitForReceived = waitForReceived;
    }

    public ManualResetEventSlim Reset { get; } = new();

    // concurrent queues allow lock-free, thread-safe enumeration in tests
    private readonly ConcurrentQueue<object> _stats = new();
    private readonly ConcurrentQueue<object> _posted = new();
    private readonly ConcurrentQueue<object> _received = new();

    // snapshot queues as arrays to keep API compatible with IReadOnlyList
    public IReadOnlyList<object> Stats => _stats.ToArray();
    public IReadOnlyList<object> Posted => _posted.ToArray();
    public IReadOnlyList<object> Received => _received.ToArray();

    public void MailboxStarted() => _stats.Enqueue("Started");

    public void MessagePosted(object message)
    {
        _stats.Enqueue(message);
        _posted.Enqueue(message);
    }

    public void MessageReceived(object message)
    {
        _stats.Enqueue(message);
        _received.Enqueue(message);

        if (_waitForReceived is not null && _waitForReceived(message))
        {
            Reset.Set();
        }
    }

    public void MailboxEmpty() => _stats.Enqueue("Empty");

    /// <summary>
    /// Asynchronously waits until <see cref="Reset"/> is signaled or the <paramref name="timeout"/> elapses.
    /// </summary>
    public Task WaitForResetAsync(TimeSpan timeout) =>
        TestKit.AwaitConditionAsync(() => Reset.IsSet, timeout);
}