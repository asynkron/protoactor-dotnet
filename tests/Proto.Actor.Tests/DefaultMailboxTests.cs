using Proto.Mailbox;
using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Tests
{
    public class DefaultMailboxTests
    {
        public void test()
        {
            var test = new DefaultMailbox(new UnboundedMailboxQueue(), new UnboundedMailboxQueue());
        }
    }
}
