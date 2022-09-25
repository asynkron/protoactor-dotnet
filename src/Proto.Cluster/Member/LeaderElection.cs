// -----------------------------------------------------------------------
// <copyright file="LeaderElection.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;
using System.Linq;

namespace Proto.Cluster;

public static class LeaderElection
{
    public static string Elect(ImmutableDictionary<string, ClusterTopologyNotification> memberState) =>
        memberState
            .Values
            .Where(m => memberState.ContainsKey(m.LeaderId))
            .GroupBy(m => m.LeaderId)
            .Select(g => (Id: g.Key, Score: g.Count()))
            .OrderByDescending(t => t.Score)
            .ThenBy(t => t.Id)
            .Select(t => t.Id)
            .FirstOrDefault() ?? memberState.Values.OrderBy(m => m.MemberId).First().MemberId;
}

public record LeaderElected(Member Leader);