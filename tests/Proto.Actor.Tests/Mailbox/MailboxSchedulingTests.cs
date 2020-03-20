using Proto.TestFixtures;
using System;
using System.Threading.Tasks;
using Xunit;
using TestMessage = Proto.TestFixtures.TestMessage;

namespace Proto.Mailbox.Tests
{
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

            var msg1 = new TestMessage();
            var msg2 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            mailbox.PostUserMessage(msg2);
            Assert.True(userMailbox.HasMessages, "Mailbox should not have processed msg2 because processing of msg1 is not completed.");

            Action resumeMailboxTrigger = () =>
            {
                // mailbox is waiting on msg1 to be completed before continuing
                // setting msg2 first guarantees that both messages will be processed
                msg2.TaskCompletionSource.SetResult(0);
                msg1.TaskCompletionSource.SetResult(0);
            };

            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

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
        public async Task GivenNonCompletedSystemMessage_ShouldHaltProcessingUntilCompletion()
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

            void ResumeMailboxTrigger()
            {
                // mailbox is waiting on msg1 to be completed before continuing
                // setting msg2 first guarantees that both messages will be processed
                msg2.TaskCompletionSource.SetResult(0);
                msg1.TaskCompletionSource.SetResult(0);
            }

            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(ResumeMailboxTrigger)
                .ConfigureAwait(false);

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

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldSetMailboxToIdleAfterCompletion()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            mailbox.PostUserMessage(msg1);

            void ResumeMailboxTrigger() => msg1.TaskCompletionSource.SetResult(0);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(ResumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.True(mailbox.Status == MailboxStatus.Idle, "Mailbox should be set back to Idle after completion of message.");
        }
    }
}
