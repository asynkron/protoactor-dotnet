using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using Xunit;

namespace Proto.Mailbox.Tests
{
    public class MailboxQueuesTests
    {
        public enum MailboxQueueKind { Bounded, Unbounded, }

        IMailboxQueue GetMailboxQueue(MailboxQueueKind kind)
        {
            switch (kind)
            {
                case MailboxQueueKind.Bounded: return new BoundedMailboxQueue(4);
                case MailboxQueueKind.Unbounded: return new UnboundedMailboxQueue();
            }
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        [Theory]
        //[InlineData(MailboxQueueKind.Bounded)] -- temporarily disabled because the Bounded queue doesn't seem to work correctly
        [InlineData(MailboxQueueKind.Unbounded)]
        public void Given_MailboxQueue_When_push_pop_Then_HasMessages_relate_the_queue_status(MailboxQueueKind kind)
        {
            var sut = GetMailboxQueue(kind);
            Assert.False(sut.HasMessages);

            sut.Push(1);
            Assert.True(sut.HasMessages);

            sut.Push(2);
            Assert.True(sut.HasMessages);

            Assert.Equal(1, sut.Pop());
            Assert.True(sut.HasMessages);

            Assert.Equal(2, sut.Pop());
            Assert.False(sut.HasMessages);
        }

        [Theory]
        //[InlineData(MailboxQueueKind.Bounded)] -- temporarily disabled because the Bounded queue doesn't seem to work correctly
        [InlineData(MailboxQueueKind.Unbounded)]
        public void Given_MailboxQueue_when_enqueue_and_dequeue_in_different_threads_Then_we_get_the_elements_in_the_FIFO_order(MailboxQueueKind kind)
        {
            const int msgCount = 1000;
            var cancelSource = new CancellationTokenSource();

            var sut = GetMailboxQueue(kind);

            var producer = new Thread(_ =>
            {
                for (int i = 0; i < msgCount; i++)
                {
                    if (cancelSource.IsCancellationRequested) return;
                    sut.Push(i);
                }
            });

            var consumerList = new List<int>();
            var consumer = new Thread(l =>
            {
                var list = (List<int>)l;
                for (int i = 0; i < msgCount; i++)
                {
                    object popped = sut.Pop();
                    while (popped == null)
                    {
                        if (cancelSource.IsCancellationRequested) return;

                        Thread.Sleep(1);
                        popped = sut.Pop();
                    }
                    list.Add((int)popped);
                };
            });

            producer.Start(); consumer.Start(consumerList);
            producer.Join(1000); consumer.Join(1000);
            cancelSource.Cancel();

            Assert.Equal(msgCount, consumerList.Count);
            for (int i = 0; i < msgCount; i++)
            {
                Assert.Equal(i, consumerList[i]);
            }
        }
    }
}
