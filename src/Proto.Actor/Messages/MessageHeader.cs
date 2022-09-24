// -----------------------------------------------------------------------
// <copyright file="MessageHeader.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using JetBrains.Annotations;

// ReSharper disable once CheckNamespace
namespace Proto;

/// <summary>
///     A collection of message headers
/// </summary>
[PublicAPI]
public record MessageHeader : IReadOnlyDictionary<string, string>
{
    public static readonly MessageHeader Empty = new(ImmutableDictionary<string, string>.Empty);

    public MessageHeader(IDictionary<string, string> headers)
    {
        Inner = headers.ToImmutableDictionary();
    }

    private ImmutableDictionary<string, string> Inner { get; init; }

    public IEnumerator<KeyValuePair<string, string>> GetEnumerator()
    {
        return Inner.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return Inner.GetEnumerator();
    }

    public int Count => Inner.Count;

    public bool ContainsKey(string key)
    {
        return Inner.ContainsKey(key);
    }

    public bool TryGetValue(string key, out string value)
    {
        return Inner.TryGetValue(key, out value!);
    }

    public string this[string key] => Inner[key];

    public IEnumerable<string> Keys => Inner.Keys;
    public IEnumerable<string> Values => Inner.Values;

    public IDictionary<string, string> ToDictionary()
    {
        return Inner;
    }

    public string? GetOrDefault(string key, string? @default = null)
    {
        return TryGetValue(key, out var value) ? value : @default;
    }

    public MessageHeader With(string key, string value)
    {
        return this with { Inner = Inner.SetItem(key, value) };
    }

    public MessageHeader With(IEnumerable<KeyValuePair<string, string>> items)
    {
        return this with { Inner = Inner.SetItems(items) };
    }

    public MessageHeader With(MessageHeader header)
    {
        if (header.Count == 0)
        {
            return this;
        }

        if (Count == 0)
        {
            return header;
        }

        return this with { Inner = Inner.SetItems(header.Inner) };
    }
}