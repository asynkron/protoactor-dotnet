// -----------------------------------------------------------------------
//   <copyright file="MessageHeader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;

namespace Proto
{
    public class MessageHeader : IReadOnlyDictionary<string, string>
    {
        private readonly Dictionary<string, string> _inner;
        public static MessageHeader Empty => new MessageHeader();

        public IDictionary<string, string> ToDictionary()
        {
            return _inner;
        }

        public MessageHeader()
        {
            _inner = new Dictionary<string, string>();
        }

        public MessageHeader(IDictionary<string,string> headers)
        {
            _inner = new Dictionary<string, string>(headers);
        }

        public string GetOrDefault(string key, string @default = null)
        {
            return TryGetValue(key, out var value) ? value : @default;
        }

        public MessageHeader With(string key, string value)
        {
            var copy = new Dictionary<string, string>(_inner) {[key] = value};
            return new MessageHeader(copy);
        }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() => _inner.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _inner.GetEnumerator();
        public int Count => _inner.Count;
        public bool ContainsKey(string key) => _inner.ContainsKey(key);

        public bool TryGetValue(string key, out string value) => _inner.TryGetValue(key, out value);

        public string this[string key] => _inner[key];

        public IEnumerable<string> Keys => _inner.Keys;
        public IEnumerable<string> Values => _inner.Values;
    }
}