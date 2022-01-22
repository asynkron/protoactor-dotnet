// -----------------------------------------------------------------------
// <copyright file="Extensions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;

namespace Proto.Cluster.Gossip
{
    public static class Extensions
    {
        public static ClusterTopology? GetTopology(this GossipState.Types.GossipMemberState memberState)
        {
            if (!memberState.Values.TryGetValue("topology", out var entry))
                return null;

            var topology = entry.Value.Unpack<ClusterTopology>();
            return topology;
        }

        internal static (bool, T?) HasConsensus<T>(this IEnumerable<T?> enumerable)
        {
            using var enumerator = enumerable.GetEnumerator();
            if (!enumerator.MoveNext() || enumerator.Current is null) return default;

            var first = enumerator.Current;

            while (enumerator.MoveNext())
            {
                if (enumerator.Current?.Equals(first) != true) return default;
            }

            return (true, first);
        }
    }
}