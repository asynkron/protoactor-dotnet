using System;
using Proto.TestFixtures;
using Xunit;
using System.Threading.Tasks;

namespace Proto.Mailbox.Tests
{
    public class MailboxStatisticsTests
    {
        private readonly TestMailboxHandler _mailboxHandler;
        private readonly TestMailboxStatistics _mailboxStatistics;
        private readonly DefaultMailbox _mailbox;

        public MailboxStatisticsTests()
        {
            _mailboxHandler = new TestMailboxHandler();
            var userMailbox = new UnboundedMailboxQueue();
            var systemMessages = new UnboundedMailboxQueue();
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
            var msg1 = new TestMessage();

            _mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenSystemMessage_ShouldInvokeMessagePosted()
        {
            var msg1 = new TestMessage();

            _mailbox.PostSystemMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldInvokeMessageReceivedAfterCompletion()
        {
            var msg1 = new TestMessage();

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
            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetResult(0);

            _mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessage();

            _mailbox.PostUserMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostUserMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessage();

            _mailbox.PostSystemMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(new Exception());
            await _mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessage();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostSystemMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }
    }
}