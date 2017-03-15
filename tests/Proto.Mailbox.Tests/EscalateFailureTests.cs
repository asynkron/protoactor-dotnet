using System;
using System.Threading;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class EscalateFailureTests
    {
        [Fact]
        public void GivenCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostUserMessage(msg1);

            Assert.Equal(1, mailboxHandler.EscalatedFailures.Count);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostSystemMessage(msg1);

            Assert.Equal(1, mailboxHandler.EscalatedFailures.Count);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenNonCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            Thread.Sleep(500);
            Assert.Equal(1, mailboxHandler.EscalatedFailures.Count);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            Thread.Sleep(500);
            Assert.Equal(1, mailboxHandler.EscalatedFailures.Count);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }
    }
}
