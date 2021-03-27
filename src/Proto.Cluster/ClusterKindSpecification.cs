// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster
{
    public record ClusterKindSpecification(string Name, Props Props)
    {
        public Func<Cluster, IMemberStrategy>? StrategyBuilder { get; init; }

        public ClusterKindSpecification WithMemberStrategy(Func<Cluster, IMemberStrategy> strategyBuilder)
            => this with {StrategyBuilder = strategyBuilder};

        internal ClusterKind Build(Cluster cluster) => new(Name, Props, StrategyBuilder?.Invoke(cluster) ?? new SimpleMemberStrategy());
    }
}