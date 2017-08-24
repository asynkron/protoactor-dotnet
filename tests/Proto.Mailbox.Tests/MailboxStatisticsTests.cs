using System;
using Proto.TestFixtures;
using Xunit;
using System.Threading.Tasks;

namespace Proto.Mailbox.Tests
{
    public class MailboxStatisticsTests
    {
        [Fact]
        public void GivenMailboxStarted_ShouldInvokeMailboxStarted()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            mailbox.Start();

            Assert.Contains("Started", mailboxStatistics.Stats);
        }

        [Fact]
        public void GivenUserMessage_ShouldInvokeMessagePosted()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenSystemMessage_ShouldInvokeMessagePosted()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);
            Assert.Contains(msg1, mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldInvokeMessageReceivedAfterCompletion()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            Assert.DoesNotContain(msg1, mailboxStatistics.Received);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetResult(0);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Contains(msg1, mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenCompletedUserMessage_ShouldInvokeMessageReceivedImmediately()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);

            mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            mailbox.PostUserMessage(msg1);

            Assert.DoesNotContain(msg1, mailboxStatistics.Received);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
            var mailboxStatistics = new TestMailboxStatistics();
            var mailbox = new DefaultMailbox(systemMessages, userMailbox, mailboxStatistics);
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            mailbox.PostSystemMessage(msg1);

            Assert.DoesNotContain(msg1, mailboxStatistics.Received);
        }
    }
}