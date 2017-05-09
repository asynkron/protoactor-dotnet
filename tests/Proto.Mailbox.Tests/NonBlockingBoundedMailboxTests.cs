using System;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class NonBlockingBoundedMailboxTests 
    {
        [Fact]
        public void WhenMailboxOverflows_OverflowActionCalledWithMessage()
        {
            object overflowMessage = null;
            var mailbox = new NonBlockingBoundedMailbox(1, (msg) => overflowMessage = msg, TimeSpan.FromSeconds(1));
            mailbox.Push("first message");
            Assert.Null(overflowMessage);
            var secondMessage = "second message";
            mailbox.Push(secondMessage);
            Assert.Equal(overflowMessage, secondMessage);
        }

        [Fact]
        public void WhenMailboxOverflows_OverflowActionCalledOnAllSubsequentMessages()
        {
            int overflowActionCallCount = 0;
            var mailbox = new NonBlockingBoundedMailbox(1, (msg) => overflowActionCallCount++, TimeSpan.FromSeconds(1));
            mailbox.Push("first message"); // does not call overflow
            for (int i = 0; i < 10; i++) {
                mailbox.Push(i);
            }
            
            Assert.Equal(overflowActionCallCount, 10);
        }

        [Fact]
        public void WhenMailboxOverflows_CurrentMessagesRemainInMailbox()
        {
            object overflowMessage = null;
            var mailbox = new NonBlockingBoundedMailbox(1, (msg) => overflowMessage = msg, TimeSpan.FromSeconds(1));
            mailbox.Push("first message");
            mailbox.Push("second message");
            Assert.Equal("first message", mailbox.Pop());
        }
    }
}