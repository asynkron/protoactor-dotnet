using System.Threading;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Mailbox.Tests;

public class MailboxSchedulingTests
{
    [Fact]
    public async Task GivenNonCompletedUserMessage_ShouldHaltProcessingUntilCompletion()
    {
        var mailboxHandler = new TestMailboxHandler();
        var userMailbox = new UnboundedMailboxQueue();
        var systemMessages = new UnboundedMailboxQueue();
        var mailbox = new DefaultMailbox(systemMessages, userMailbox);
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var msg2 = new TestMessageWithTaskCompletionSource();

        mailbox.PostUserMessage(msg1);
        mailbox.PostUserMessage(msg2);

        Assert.True(userMailbox.HasMessages,
            "Mailbox should not have processed msg2 because processing of msg1 is not completed.",
        );

        msg2.TaskCompletionSource.SetResult(0);
        msg1.TaskCompletionSource.SetResult(0);
        await Task.WhenAll(msg2.TaskCompletionSource.Task, msg1.TaskCompletionSource.Task);
        SpinWait.SpinUntil(() => !userMailbox.HasMessages, 1000);

        Assert.False(userMailbox.HasMessages,
            "Mailbox should have processed msg2 because processing of msg1 is completed.",
        );
    }

    [Fact]
    public async Task GivenCompletedUserMessage_ShouldContinueProcessing()
    {
        var mailboxHandler = new TestMailboxHandler();
        var userMailbox = new UnboundedMailboxQueue();
        var systemMessages = new UnboundedMailboxQueue();
        var mailbox = new DefaultMailbox(systemMessages, userMailbox);
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var msg2 = new TestMessageWithTaskCompletionSource();
        msg1.TaskCompletionSource.SetResult(0);
        msg2.TaskCompletionSource.SetResult(0);

        mailbox.PostUserMessage(msg1);
        mailbox.PostUserMessage(msg2);

        await Task.WhenAll(msg1.TaskCompletionSource.Task, msg2.TaskCompletionSource.Task);

        Assert.False(userMailbox.HasMessages,
            "Mailbox should have processed both messages because they were already completed.",
        );
    }

    [Fact]
    public async Task GivenNonCompletedSystemMessage_ShouldHaltProcessingUntilCompletion()
    {
        var mailboxHandler = new TestMailboxHandler();
        var userMailbox = new UnboundedMailboxQueue();
        var systemMessages = new UnboundedMailboxQueue();
        var mailbox = new DefaultMailbox(systemMessages, userMailbox);
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var msg2 = new TestMessageWithTaskCompletionSource();

        mailbox.PostSystemMessage(msg1);
        mailbox.PostSystemMessage(msg2);

        Assert.True(systemMessages.HasMessages,
            "Mailbox should not have processed msg2 because processing of msg1 is not completed.",
        );

        msg2.TaskCompletionSource.SetResult(0);
        msg1.TaskCompletionSource.SetResult(0);
        await Task.WhenAll(msg2.TaskCompletionSource.Task, msg1.TaskCompletionSource.Task);
        SpinWait.SpinUntil(() => !systemMessages.HasMessages, 1000);

        Assert.False(systemMessages.HasMessages,
            "Mailbox should have processed msg2 because processing of msg1 is completed.",
        );
    }

    [Fact]
    public async Task GivenCompletedSystemMessage_ShouldContinueProcessing()
    {
        var mailboxHandler = new TestMailboxHandler();
        var userMailbox = new UnboundedMailboxQueue();
        var systemMessages = new UnboundedMailboxQueue();
        var mailbox = new DefaultMailbox(systemMessages, userMailbox);
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        var msg2 = new TestMessageWithTaskCompletionSource();
        msg1.TaskCompletionSource.SetResult(0);
        msg2.TaskCompletionSource.SetResult(0);

        mailbox.PostSystemMessage(msg1);
        mailbox.PostSystemMessage(msg2);
        await Task.WhenAll(msg1.TaskCompletionSource.Task, msg2.TaskCompletionSource.Task);

        Assert.False(systemMessages.HasMessages,
            "Mailbox should have processed both messages because they were already completed.",
        );
    }

    [Fact]
    public async Task GivenNonCompletedUserMessage_ShouldSetMailboxToIdleAfterCompletion()
    {
        var mailboxHandler = new TestMailboxHandler();
        var userMailbox = new UnboundedMailboxQueue();
        var systemMessages = new UnboundedMailboxQueue();
        var mailbox = new DefaultMailbox(systemMessages, userMailbox);
        mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

        var msg1 = new TestMessageWithTaskCompletionSource();
        mailbox.PostUserMessage(msg1);

        Assert.Equal(MailboxStatus.Busy, mailbox.Status);
        msg1.TaskCompletionSource.SetResult(0);
        await msg1.TaskCompletionSource.Task;
        SpinWait.SpinUntil(() => mailbox.Status == MailboxStatus.Idle, 1000);

        Assert.True(mailbox.Status == MailboxStatus.Idle,
            "Mailbox should be set back to Idle after completion of message.",
        );
    }
}

