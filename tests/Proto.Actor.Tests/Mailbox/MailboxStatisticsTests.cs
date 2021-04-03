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
        public async Task GivenUserMessage_ShouldInvokeMessagePosted()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();

            _mailbox.PostUserMessage(msg1);
            await Task.Delay(1000);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenSystemMessage_ShouldInvokeMessagePosted()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();

            _mailbox.PostSystemMessage(msg1);
            await Task.Delay(1000);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessage_ShouldInvokeMessageReceivedAfterCompletion()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();

            _mailbox.PostUserMessage(msg1);
            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
            msg1.TaskCompletionSource.SetResult(0);
            await Task.Delay(1000);

            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public void GivenCompletedUserMessage_ShouldInvokeMessageReceivedImmediately()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();
            msg1.TaskCompletionSource.SetResult(0);

            _mailbox.PostUserMessage(msg1);
            Assert.Contains(msg1, _mailboxStatistics.Posted);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();

            _mailbox.PostUserMessage(msg1);

            msg1.TaskCompletionSource.SetException(new Exception());
            await Task.Delay(1000);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedUserMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostUserMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();

            _mailbox.PostSystemMessage(msg1);
            msg1.TaskCompletionSource.SetException(new Exception());
            await Task.Delay(1000);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }

        [Fact]
        public void GivenCompletedSystemMessageThrewException_ShouldNotInvokeMessageReceived()
        {
            var msg1 = new TestMessageWithTaskCompletionSource();
            msg1.TaskCompletionSource.SetException(new Exception());

            _mailbox.PostSystemMessage(msg1);

            Assert.DoesNotContain(msg1, _mailboxStatistics.Received);
        }
    }
}