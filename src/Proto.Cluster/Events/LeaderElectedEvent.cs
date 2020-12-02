// -----------------------------------------------------------------------
// <copyright file="LeaderElectedEvent.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using JetBrains.Annotations;
using Proto.Cluster.Data;

namespace Proto.Cluster.Events
{
    [PublicAPI]
    public class LeaderElectedEvent
    {
        public LeaderElectedEvent(LeaderInfo? newLeader, LeaderInfo? oldLeader)
        {
            NewLeader = newLeader;
            OldLeader = oldLeader;
        }

        public LeaderInfo? NewLeader { get; }
        public LeaderInfo? OldLeader { get; }
    }
}