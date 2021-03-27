// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster
{
    public record ClusterKind(string Name, Props Props)
    {
        public Func<Cluster, IMemberStrategy>? StrategyBuilder { get; init; }

        public ClusterKind WithMemberStrategy(Func<Cluster, IMemberStrategy> strategyBuilder)
            => this with {StrategyBuilder = strategyBuilder};

        internal ActivatedClusterKind Build(Cluster cluster) => new(Name, Props, StrategyBuilder?.Invoke(cluster) ?? new SimpleMemberStrategy());
    }
}