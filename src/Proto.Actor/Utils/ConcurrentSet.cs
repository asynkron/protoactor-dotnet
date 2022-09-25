// -----------------------------------------------------------------------
// <copyright file="ConcurrentSet.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Concurrent;
using System.Linq;
using JetBrains.Annotations;

namespace Proto.Utils;

/// <summary>
///     A collection with set semantics built on top of <see cref="ConcurrentDictionary{TKey,TValue}" />.
/// </summary>
/// <typeparam name="T"></typeparam>
[PublicAPI]
public class ConcurrentSet<T> where T : notnull
{
    private readonly ConcurrentDictionary<T, byte> _inner = new();

    public bool Contains(T key) => _inner.ContainsKey(key);

    public void Add(T key) => _inner.TryAdd(key, 1);

    public bool TryAdd(T key) => _inner.TryAdd(key, 1);

    public void Remove(T key) => _inner.TryRemove(key, out _);

    public T[] ToArray() => _inner.Keys.ToArray();
}