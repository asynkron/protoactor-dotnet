using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : Dictionary<string, string>
    {
        internal static readonly MessageHeader EmotyHeader = new MessageHeader();

        public string GetOrDefault(string key, string @default = null)
        {
            if (ContainsKey(key))
            {
                return this[key];
            }
            return @default;
        }
    }
}