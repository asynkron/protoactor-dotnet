// -----------------------------------------------------------------------
// <copyright file="GossipDeltaValue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Gossip
{
    public partial class GossipDeltaValue : IDeltaValue
    {
        public GossipDeltaValue GetDelta(long fromSequenceNumber)
        {
            var delta = new GossipDeltaValue();

            //TODO: optimize this, binary search and all that...
            foreach (var entry in Entries)
            {
                if (entry.SequenceNumber < fromSequenceNumber)
                    continue;

                delta.Entries.Add(entry);
            }

            return delta;
        }
    }
}