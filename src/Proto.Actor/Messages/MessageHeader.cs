// -----------------------------------------------------------------------
// <copyright file="MessageHeader.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Proto
{
    [PublicAPI]
    public record MessageHeader : IReadOnlyDictionary<string, string>
    {
        public static readonly MessageHeader Empty = new(ImmutableDictionary<string, string>.Empty);

        public MessageHeader(IDictionary<string, string> headers) => Inner = headers.ToImmutableDictionary();

        private ImmutableDictionary<string, string> Inner { get; init; }

        public IEnumerator<KeyValuePair<string, string>> GetEnumerator() =>
            Inner.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            Inner.GetEnumerator();

        public int Count => Inner.Count;

        public bool ContainsKey(string key) => Inner.ContainsKey(key);

        public bool TryGetValue(string key, out string value) =>
            Inner.TryGetValue(key, out value);

        public string this[string key] => Inner[key];

        public IEnumerable<string> Keys => Inner.Keys;
        public IEnumerable<string> Values => Inner.Values;

        public IDictionary<string, string> ToDictionary() => Inner;

        public string? GetOrDefault(string key, string? @default = null) =>
            TryGetValue(key, out var value) ? value : @default;

        public MessageHeader With(string key, string value) =>
            this with {Inner = Inner.SetItem(key, value)};

        public MessageHeader With(IEnumerable<KeyValuePair<string, string>> items) =>
            this with {Inner = Inner.SetItems(items)};
    }
}