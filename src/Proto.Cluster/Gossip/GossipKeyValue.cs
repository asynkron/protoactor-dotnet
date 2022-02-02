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
        public DateTimeOffset LocalTimestamp => DateTimeOffset.FromUnixTimeMilliseconds(LocalTimestampUnixMilliseconds);
        public TimeSpan Age => DateTimeOffset.UtcNow - LocalTimestamp;
    }
}