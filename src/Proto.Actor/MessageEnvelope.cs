using System.Collections;
using System.Collections.Generic;

namespace Proto
{
    public class MessageEnvelope
    {
        public PID Sender { get; set; }
        public object Message { get; set; }
        public MessageHeader Header { get; set; }
    }

    public class MessageHeader : Dictionary<string,string>
    {
    }
}
