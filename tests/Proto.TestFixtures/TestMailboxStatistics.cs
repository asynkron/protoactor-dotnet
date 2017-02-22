using System.Collections.Generic;
using Proto.Mailbox;

namespace Proto.TestFixtures
{
    public class TestMailboxStatistics : IMailboxStatistics
    {
        public List<object> Stats { get; } = new List<object>();
        public List<object> Posted { get; } = new List<object>();
        public List<object> Received { get; } = new List<object>();

        public void MailboxStarted()
        {
            Stats.Add("Started");
        }

        public void MessagePosted(object message)
        {
            Stats.Add(message);
            Posted.Add(message);
        }

        public void MessageReceived(object message)
        {
            Stats.Add(message);
            Received.Add(message);
        }

        public void MailboxEmpty()
        {
            Stats.Add("Empty");
        }
    }
}