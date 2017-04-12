using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : Dictionary<string, string>
    {
        internal static readonly MessageHeader EmotyHeader = new MessageHeader();
    }
}