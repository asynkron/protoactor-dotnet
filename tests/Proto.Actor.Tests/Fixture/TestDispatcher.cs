using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace Proto.Tests.Fixture
{
    public class TestDispatcher : IDispatcher
    {
        public int Throughput => 10;

        public void Schedule(Func<Task> runner) => runner().Wait();
    }
}
