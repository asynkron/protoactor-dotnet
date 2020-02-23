using Proto.TestFixtures;
using System;
using System.Threading.Tasks;
using Xunit;
using TestMessage = Proto.TestFixtures.TestMessage;

namespace Proto.Mailbox.Tests
{
    public class MailboxSchedulingTests
    {
        private readonly DefaultMailbox _mailbox;
        private readonly UnboundedMailboxQueue _userMailbox;
        private readonly TestMailboxHandler _mailboxHandler;
        private readonly UnboundedMailboxQueue _systemMessages;

        public MailboxSchedulingTests()
        {
            _mailboxHandler = new TestMailboxHandler();
            _userMailbox = new UnboundedMailboxQueue();
            _systemMessages = new UnboundedMailboxQueue();
            _mailbox = new DefaultMailbox(_systemMessages, _userMailbox);
            _mailbox.RegisterHandlers(_mailboxHandler, _mailboxHandler);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldHaltProcessingUntilCompletion()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();

            _mailbox.PostUserMessage(msg1);
            _mailbox.PostUserMessage(msg2);
            Assert.True(_userMailbox.HasMessages, "Mailbox should not have processed msg2 because processing of msg1 is not completed.");

            Action resumeMailboxTrigger = () =>
            {
                // mailbox is waiting on msg1 to be completed before continuing
                // setting msg2 first guarantees that both messages will be processed
                msg2.TaskCompletionSource.SetResult(0);
                msg1.TaskCompletionSource.SetResult(0);
            };

            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.False(_userMailbox.HasMessages, "Mailbox should have processed msg2 because processing of msg1 is completed.");
        }

        [Fact]
        public void GivenCompletedUserMessage_ShouldContinueProcessing()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);
            msg2.TaskCompletionSource.SetResult(0);

            _mailbox.PostUserMessage(msg1);
            _mailbox.PostUserMessage(msg2);
            Assert.False(_userMailbox.HasMessages, "Mailbox should have processed both messages because they were already completed.");
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessage_ShouldHaltProcessingUntilCompletion()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();

            _mailbox.PostSystemMessage(msg1);
            _mailbox.PostSystemMessage(msg2);
            Assert.True(_systemMessages.HasMessages, "Mailbox should not have processed msg2 because processing of msg1 is not completed.");

            Action resumeMailboxTrigger = () =>
            {
                // mailbox is waiting on msg1 to be completed before continuing
                // setting msg2 first guarantees that both messages will be processed
                msg2.TaskCompletionSource.SetResult(0);
                msg1.TaskCompletionSource.SetResult(0);
            };

            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.False(_systemMessages.HasMessages, "Mailbox should have processed msg2 because processing of msg1 is completed.");
        }

        [Fact]
        public void GivenCompletedSystemMessage_ShouldContinueProcessing()
        {
            var msg1 = new TestMessage();
            var msg2 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);
            msg2.TaskCompletionSource.SetResult(0);

            _mailbox.PostSystemMessage(msg1);
            _mailbox.PostSystemMessage(msg2);
            Assert.False(_systemMessages.HasMessages, "Mailbox should have processed both messages because they were already completed.");
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldSetMailboxToIdleAfterCompletion()
        {
            var msg1 = new TestMessage();
            _mailbox.PostUserMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetResult(0);

            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.True(_mailbox.Status == MailboxStatus.Idle, "Mailbox should be set back to Idle after completion of message.");
        }
    }
}