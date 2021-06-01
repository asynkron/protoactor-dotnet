using System;
using System.Threading.Tasks;
using Proto.TestFixtures;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class EscalateFailureTests
    {
        [Fact]
        public async Task GivenCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostUserMessage(msg1);
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e);
        }

        [Fact]
        public async Task GivenCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);

            mailbox.PostSystemMessage(msg1);
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();

            mailbox.PostUserMessage(msg1);
            var taskException = new Exception();

            await Task.Delay(10);
            msg1.TaskCompletionSource.SetException(taskException);
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageTaskThrewException_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();

            mailbox.PostSystemMessage(msg1);
            
            //make sure the message is being processed by the mailboxHandler
            //e.g. await mailboxHandler.GotMessage()
            await Task.Delay(10);

            //fail the current task being processed
            var taskException = new Exception();
            msg1.TaskCompletionSource.SetException(taskException);
            
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            var e = Assert.IsType<Exception>(mailboxHandler.EscalatedFailures[0]);
            Assert.Equal(taskException, e);
        }

        [Fact]
        public async Task GivenCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostUserMessage(msg1);
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();
            msg1.TaskCompletionSource.SetCanceled();

            mailbox.PostSystemMessage(msg1);
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenNonCompletedUserMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();

            mailbox.PostUserMessage(msg1);
            
            //this is a async message
            await Task.Delay(10);
            
            msg1.TaskCompletionSource.SetCanceled();
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }

        [Fact]
        public async Task GivenNonCompletedSystemMessageTaskGotCancelled_ShouldEscalateFailure()
        {
            var mailboxHandler = new TestMailboxHandler();
            var mailbox = UnboundedMailbox.Create();
            mailbox.RegisterHandlers(mailboxHandler, mailboxHandler);

            var msg1 = new TestMessageWithTaskCompletionSource();

            //post the test message to the mailbox
            mailbox.PostSystemMessage(msg1);
            
            //this is a async message
            await Task.Delay(10);
            msg1.TaskCompletionSource.SetCanceled();
            await mailboxHandler.HasFailures;

            Assert.Single(mailboxHandler.EscalatedFailures);
            Assert.IsType<TaskCanceledException>(mailboxHandler.EscalatedFailures[0]);
        }
    }
}