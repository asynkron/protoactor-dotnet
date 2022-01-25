// -----------------------------------------------------------------------
// <copyright file="GossipKeyValue.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Gossip
{
    public partial class GossipKeyValue
    {
        public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;

        public TimeSpan Age => DateTimeOffset.UtcNow - Timestamp;
    }
}