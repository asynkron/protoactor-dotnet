// -----------------------------------------------------------------------
// <copyright file="HashRing.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;

namespace Proto.Router;

public class HashRing<T>
{
    private readonly Func<T, string> _getKey;
    private readonly Func<string, uint> _hash;
    private readonly int _replicaCount;

    // Does not update these, replaced on mutation. Would otherwise break clone.
    private uint[] _hashes;
    private (uint hash, T value)[] _ring;

    public HashRing(IEnumerable<T> nodes, Func<T, string> getKey, Func<string, uint> hash, int replicaCount)
    {
        _getKey = getKey;
        _hash = hash;
        _replicaCount = replicaCount;

        _ring = ToHashTuples(nodes);
        _hashes = _ring.Select(it => it.hash).ToArray();
    }

    private HashRing(Func<T, string> getKey, Func<string, uint> hash, int replicaCount, (uint hash, T value)[] ring,
        uint[] hashes)
    {
        _getKey = getKey;
        _hash = hash;
        _replicaCount = replicaCount;
        _ring = ring;
        _hashes = hashes;
    }

    public int Count => _ring.Length / _replicaCount;

    public T GetNode(string key)
    {
        if (_ring.Length == 0)
        {
            return default!;
        }

        if (_replicaCount == _ring.Length)
        {
            return _ring[0].value;
        }

        var hash = _hash(key);

        var result = Array.BinarySearch(_hashes, hash);

        if (result >= 0)
        {
            return _ring[result].value;
        }

        // Get the next higher value by taking the complement of the result
        var nextIndex = ~result;

        // Return the next higher value if it exists, or the first one
        return _ring[nextIndex % _ring.Length].value;
    }

    public void Add(params T[] added) => SetRing(Merge(ToHashTuples(added), _ring));

    public void Remove(ISet<T> nodes) => SetRing(_ring.Where(it => !nodes.Contains(it.value)).ToArray());

    public void Remove(T node) => SetRing(_ring.Where(it => !node!.Equals(it.value)).ToArray());

    private void SetRing((uint hash, T value)[] ring)
    {
        _ring = ring;
        _hashes = _ring.Select(it => it.hash).ToArray();
    }

    private static (uint hash, T value)[] Merge((uint hash, T value)[] left, (uint hash, T value)[] right)
    {
        var result = new (uint hash, T value)[left.Length + right.Length];
        var i = 0;
        var j = 0;
        var k = 0;

        while (i < result.Length && j < left.Length && k < right.Length)
        {
            result[i++] = left[j].hash < right[k].hash ? left[j++] : right[k++];
        }

        while (i < result.Length && k < right.Length)
        {
            result[i++] = right[k++];
        }

        while (i < result.Length && j < left.Length)
        {
            result[i++] = left[j++];
        }

        return result;
    }

    private (uint hash, T value)[] ToHashTuples(IEnumerable<T> nodes) =>
        nodes
            .SelectMany(
                n =>
                    Enumerable
                        .Range(0, _replicaCount)
                        .Select(
                            i => new
                            {
                                hashKey = i + _getKey(n),
                                node = n
                            }
                        )
            )
            .Select(a => (_hash(a.hashKey), a.node))
            .OrderBy(t => t.Item1)
            .ToArray();

    public HashRing<T> Clone() => new(_getKey, _hash, _replicaCount, _ring, _hashes);
}