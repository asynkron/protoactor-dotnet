// -----------------------------------------------------------------------
// <copyright file="DeltaList.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using Proto.Remote;

namespace Proto.Cluster.Gossip
{
    public class AppendOnlyLog<T> 
    {
        private readonly GossipDeltaValue _inner;
        private readonly Serialization _serialization;
        private readonly List<T> _innerList = new();

        public AppendOnlyLog(GossipDeltaValue inner, Serialization serialization)
        {
            _inner = inner;
            _serialization = serialization;
        }

        public void Add(T item)
        {
            _innerList.Add(item);
            var (bytes, typename, serializerId) = _serialization.Serialize(item!);
            var entry = new GossipDeltaValue.Types.GossipDeltaEntry
            {
                Data = bytes,
                SequenceNumber = GossipStateManagement.GetNextSequenceNumber()
            };
            _inner.Entries.Add(entry);
        }

        public int Count => _innerList.Count;

        public T this[int index] => _innerList[index];
    }
}