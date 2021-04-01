using System;
using System.Threading.Tasks;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestDispatcher : IDispatcher
    {
        public int Throughput => 10;

        public void Schedule(Func<Task> runner) => runner().Wait();
    }
}
