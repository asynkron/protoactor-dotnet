using System.Collections.Generic;

namespace Proto.Mailbox.Tests
{
    public class TestMailboxStatistics : IMailboxStatistics
    {
        internal List<object> Stats { get; } = new List<object>();
        internal List<object> Posted { get; } = new List<object>();
        internal List<object> Received { get; } = new List<object>();

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