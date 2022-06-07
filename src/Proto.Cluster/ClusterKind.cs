// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using JetBrains.Annotations;

namespace Proto.Cluster;

public delegate ValueTask<bool> CanSpawnIdentity(string identity);

[PublicAPI]
public record ClusterKind(string Name, Props Props)
{
    public Func<Cluster, IMemberStrategy>? StrategyBuilder { get; init; }

    public CanSpawnIdentity? CanSpawnIdentity { get; init; }

    public ClusterKind WithMemberStrategy(Func<Cluster, IMemberStrategy> strategyBuilder)
        => this with {StrategyBuilder = strategyBuilder};
    
    public ClusterKind WithSpawnPredicate(CanSpawnIdentity spawnPredicate)
        => this with {CanSpawnIdentity = spawnPredicate};

    internal ActivatedClusterKind Build(Cluster cluster) => new(Name, Props, StrategyBuilder?.Invoke(cluster), CanSpawnIdentity);
}