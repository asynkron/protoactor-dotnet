// -----------------------------------------------------------------------
// <copyright file="TypedDictionary.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading;

namespace Proto.Utils
{
    public class TypeDictionary<TValue>
    {
        // ReSharper disable once StaticMemberInGenericType
        private static int typeIndex;
        private readonly object _lockObject = new();

        private TValue[] _values = new TValue[100];

        public void Add<TKey>(TValue value)
        {
            lock (_lockObject)
            {
                var id = TypeKey<TKey>.Id;
                if (id >= _values.Length) Array.Resize(ref _values, id * 2);

                _values[id] = value;
            }
        }

        public TValue? Get<TKey>()
        {
            var id = TypeKey<TKey>.Id;
            return id >= _values.Length ? default : _values[id];
        }

        // ReSharper disable once UnusedTypeParameter
        private static class TypeKey<TKey>
        {
            // ReSharper disable once StaticMemberInGenericType
            internal static readonly int Id = Interlocked.Increment(ref typeIndex);
        }
    }
}