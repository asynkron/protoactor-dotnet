using Xunit;

namespace Proto.Actor.Tests
{
    public class MQMCQueueTests
    {
        [Fact]
        public void Enqueue_Then_Dequeue()
        {
            var q = new MPMCQueue(4);

            q.TryEnqueue(1);
            q.TryEnqueue(2);
            q.TryEnqueue(3);
            q.TryEnqueue(4);

            object o1, o2, o3, o4;
            q.TryDequeue(out o1);
            q.TryDequeue(out o2);
            q.TryDequeue(out o3);
            q.TryDequeue(out o4);

            Assert.Equal(1, o1);
            Assert.Equal(2, o2);
            Assert.Equal(3, o3);
            Assert.Equal(4, o4);

            q.TryEnqueue(5);
            q.TryEnqueue(6);
            q.TryEnqueue(7);
            q.TryEnqueue(8);

            q.TryDequeue(out o1);
            q.TryDequeue(out o2);
            q.TryDequeue(out o3);
            q.TryDequeue(out o4);

            Assert.Equal(5, o1);
            Assert.Equal(6, o2);
            Assert.Equal(7, o3);
            Assert.Equal(8, o4);
        }
    }
}
