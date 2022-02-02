// -----------------------------------------------------------------------
// <copyright file="MemberStateDelta.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Gossip
{
    public record MemberStateDelta(string TargetMemberId, bool HasState,  GossipState State, Action CommitOffsets);
}