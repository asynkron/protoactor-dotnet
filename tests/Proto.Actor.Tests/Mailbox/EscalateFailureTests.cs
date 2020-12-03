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
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostUserMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
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

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostUserMessage(msg1);
            var taskException = new Exception();

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(taskException);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);
            var taskException = new Exception();

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetException(taskException);
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<AggregateException>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e.InnerException);
        }

        [Fact]
        public void GivenCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostUserMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public void GivenCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostSystemMessage(msg1);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

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
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessage();

            mailbox.PostSystemMessage(msg1);

            Action resumeMailboxTrigger = () => msg1.TaskCompletionSource.SetCanceled();
            await mailboxHandler.ResumeMailboxProcessingAndWaitAsync(resumeMailboxTrigger)
                .ConfigureAwait(false);

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }
    }
}