using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : Dictionary<string, string>
    {
        internal static readonly MessageHeader EmptyHeader = new MessageHeader();

        public string GetOrDefault(string key, string @default = null)
        {
            return TryGetValue(key, out string value) ? value : @default;
        }
    }
}