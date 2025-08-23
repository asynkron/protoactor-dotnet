using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Proto.TestKit;
using Xunit;
using static Proto.TestKit.TestKit;

namespace Proto.Mailbox.Tests;

public class EscalateFailureTests
{
    [Fact]
    public async Task GivenCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var taskException = new Exception();
        msg1.TaskCompletionSource.SetException(taskException);

        mailbox.PostUserMessage(msg1);
        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
        Assert.Equal(taskException, e);
    }

    [Fact]
    public async Task GivenCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var taskException = new Exception();
        msg1.TaskCompletionSource.SetException(taskException);

        mailbox.PostSystemMessage(msg1);
        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
        Assert.Equal(taskException, e);
    }

    [Fact]
    public async Task GivenNonCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();

        mailbox.PostUserMessage(msg1);
        var taskException = new Exception();

        _ = Task.Run(() => msg1.TaskCompletionSource.SetException(taskException));

        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
        Assert.Equal(taskException, e);
    }

    [Fact]
    public async Task GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();

        mailbox.PostSystemMessage(msg1);

        //fail the current task being processed
        var taskException = new Exception();
        _ = Task.Run(() => msg1.TaskCompletionSource.SetException(taskException));

        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
        Assert.Equal(taskException, e);
    }

    [Fact]
    public async Task GivenCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        msg1.TaskCompletionSource.SetCanceled();

        mailbox.PostUserMessage(msg1);
        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
    }

    [Fact]
    public async Task GivenCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        msg1.TaskCompletionSource.SetCanceled();

        mailbox.PostSystemMessage(msg1);
        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
    }

    [Fact]
    public async Task GivenNonCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();

        mailbox.PostUserMessage(msg1);

        _ = Task.Run(() => msg1.TaskCompletionSource.SetCanceled());

        await AwaitConditionAsync(
            () => mailboxHandler.EscalatedFailures.Count == 1,
            // allow additional time for the asynchronous cancellation to propagate
            TimeSpan.FromMilliseconds(500));

        Assert.Single(mailboxHandler.EscalatedFailures);
        Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
    }

    [Fact]
    public async Task GivenNonCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
    {
        var mailboxHandler = new TestMailboxHandler();
        var mailbox = UnboundedMailbox.Create();
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();

        //post the test message to the mailbox
        mailbox.PostSystemMessage(msg1);

        _ = Task.Run(() => msg1.TaskCompletionSource.SetCanceled());

        await AwaitConditionAsync(() => mailboxHandler.EscalatedFailures.Count == 1,
            TimeSpan.FromMilliseconds(100));

        Assert.Single(mailboxHandler.EscalatedFailures);
        Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
    }
}
