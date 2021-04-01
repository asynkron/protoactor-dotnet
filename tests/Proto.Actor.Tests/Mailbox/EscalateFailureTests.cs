using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class EscalateFailureTests
    {
        [Fact]
        public void GivenCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();
            Exception taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostUserMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            AggregateException e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();
            Exception taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostSystemMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            AggregateException e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            Exception taskException = new Exception();

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(taskException);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            AggregateException e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);
            Exception taskException = new Exception();

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(taskException);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            AggregateException e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();
            Exception taskException = new Exception();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostUserMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public void GivenCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();
            Exception taskException = new Exception();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostSystemMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetCanceled();
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            TestMailboxHandler mailboxHandler = new TestMailboxHandler();
            IMailbox mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            TestMessage msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetCanceled();
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }
    }
}
