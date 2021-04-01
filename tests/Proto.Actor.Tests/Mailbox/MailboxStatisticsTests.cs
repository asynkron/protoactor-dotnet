using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class MailboxStatisticsTests
    {
        private readonly DefaultMailbox _mailbox;
        private readonly TestMailboxHandler _mailboxHandler;
        private readonly TestMailboxStatistics _mailboxStatistics;

        public MailboxStatisticsTests()
        {
            _mailboxHandler = new TestMailboxHandler();
            UnboundedMailboxQueue userMailbox = new UnboundedMailboxQueue();
            UnboundedMailboxQueue systemMessages = new UnboundedMailboxQueue();
            _mailboxStatistics = new TestMailboxStatistics();
            _mailbox = new DefaultMailbox(systemMessages, userMailbox, _mailboxStatistics);
            _mailbox.RegisterHandlers(_mailboxHandler, _mailboxHandler);
        }

        [Fact]
        public void GivenMailboxStarted_ShouldInvokeMailboxStarted()
        {
            _mailbox.Start();

            Assert.Contains("Started", _mailboxStatistics.Stats);
        }

        [Fact]
        public void GivenUserMessage_ShouldInvokeMessagePosted()
        {
            TestMessage msg1 = new TestMessage();

            _mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenSystemMessage_ShouldInvokeMessagePosted()
        {
            TestMessage msg1 = new TestMessage();

            _mailbox.PostSystemMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldInvokeMessageReceivedAfterCompletion()
        {
            TestMessage msg1 = new TestMessage();

            _mailbox.PostUserMessage(msg1);
            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetResult(0);
            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenCompletedUserMessage_ShouldInvokeMessageReceivedImmediately()
        {
            TestMessage msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);

            _mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            TestMessage msg1 = new TestMessage();

            _mailbox.PostUserMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            TestMessage msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostUserMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            TestMessage msg1 = new TestMessage();

            _mailbox.PostSystemMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            TestMessage msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostSystemMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }
    }
}
