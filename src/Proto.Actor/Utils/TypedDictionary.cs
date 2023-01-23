// -----------------------------------------------------------------------
// <copyright file="TypedDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading;

namespace Proto.Utils;

// ReSharper disable once UnusedTypeParameter
internal class TypeDictionary<TValue, TNamespace>
{
    // ReSharper disable once StaticMemberInGenericType
    private static int typeIndex = -1;
    private readonly double _growthFactor;
    private readonly object _lockObject = new();

    private TValue[] _values;

    public TypeDictionary(int initialSize = 100, double growthFactor = 2)
    {
        _values = new TValue[initialSize];
        _growthFactor = growthFactor >= 1 ? growthFactor : 1;
    }

    //TODO: Set on ActorContext, does it need locking?
    //Can we get around this?
    public void Add<TKey>(TValue value)
    {
        lock (_lockObject)
        {
            var id = TypeKey<TKey>.Id;

            if (id >= _values.Length)
            {
                Array.Resize(ref _values, (int)((id + 1) * _growthFactor));
            }

            _values[id] = value;
        }
    }

    public TValue? Get<TKey>()
    {
        var id = TypeKey<TKey>.Id;

        return id >= _values.Length ? default : _values[id];
    }

    public void Remove<TKey>()
    {
        var id = TypeKey<TKey>.Id;

        if (id >= _values.Length)
        {
            return;
        }

        _values[id] = default!;
    }

    // ReSharper disable once UnusedTypeParameter
    private static class TypeKey<TKey>
    {
        // ReSharper disable once StaticMemberInGenericType
        internal static readonly int Id = Interlocked.Increment(ref typeIndex);
    }
}