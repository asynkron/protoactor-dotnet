using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Sdk;

namespace Proto.Mailbox.Tests
{
    public class MailboxSchedulingTests
    {
        [Fact]
        public void GivenNonCompletedUserMessage_ShouldHaltProcessingUntilCompletion()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            mailbox.PostUserMessage(msg2);
            Assert.True(userMailbox.HasMessages, "Mailbox should not have processed msg2 because processing of msg1 is not completed.");

            msg1.TaskCompletionSource.SetResult(0);
            Thread.Sleep(10);
            Assert.False(userMailbox.HasMessages, "Mailbox should have processed msg2 because processing of msg1 is completed.");
        }

        [Fact]
        public void GivenCompletedUserMessage_ShouldContinueProcessing()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);
            msg2.TaskCompletionSource.SetResult(0);

            mailbox.PostUserMessage(msg1);
            mailbox.PostUserMessage(msg2);
            Assert.False(userMailbox.HasMessages, "Mailbox should have processed both messages because they were already completed.");
        }

        [Fact]
        public void GivenNonCompletedSystemMessage_ShouldHaltProcessingUntilCompletion()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();

            mailbox.PostSystemMessage(msg1);
            mailbox.PostSystemMessage(msg2);
            Assert.True(systemMessages.HasMessages, "Mailbox should not have processed msg2 because processing of msg1 is not completed.");

            msg1.TaskCompletionSource.SetResult(0);
            Thread.Sleep(10);
            Assert.False(systemMessages.HasMessages, "Mailbox should have processed msg2 because processing of msg1 is completed.");
        }

        [Fact]
        public void GivenCompletedSystemMessage_ShouldContinueProcessing()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);
            msg2.TaskCompletionSource.SetResult(0);

            mailbox.PostSystemMessage(msg1);
            mailbox.PostSystemMessage(msg2);
            Assert.False(systemMessages.HasMessages, "Mailbox should have processed both messages because they were already completed.");
        }
    }
}
