using System;
using System.Collections.Generic;
using System.Text;

namespace Proto.Scheduling
{
    public class DelayMessageComparer : IComparer<DelayMessage>
    {
        public int Compare(DelayMessage x, DelayMessage y) => x.Timeout.CompareTo(y.Timeout);
    }

    public class DelayMessage
    {
        public PID Target { get; set; }
        public object Message { get; set; }
        public DateTime Timeout { get; set; }
    }
}
