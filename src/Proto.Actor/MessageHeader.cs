// -----------------------------------------------------------------------
//   <copyright file="MessageHeader.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace Proto
{
    public class MessageHeader : IReadOnlyDictionary<string, string>
    {
        
        private readonly ImmutableDictionary<string, string> _inner;
        public static MessageHeader Empty => new MessageHeader();

        public IDictionary<string, string> ToDictionary() => _inner;

        public MessageHeader() => _inner = ImmutableDictionary<string, string>.Empty;

        public MessageHeader(IDictionary<string,string> headers) => _inner = headers.ToImmutableDictionary();

        public string GetOrDefault(string key, string @default = null) => TryGetValue(key, out var value) ? value : @default;

        public MessageHeader With(string key, string value)
        {
            var copy = _inner.SetItem(key, value);
            return new MessageHeader(copy);
        }

        public MessageHeader With(IEnumerable<KeyValuePair<string, string>> items)
        {
            var copy = _inner.SetItems(items);
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