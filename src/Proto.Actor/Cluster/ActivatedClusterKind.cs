// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;

namespace Proto.Cluster;

public record ActivatedClusterKind
{
    private long _count;

    internal ActivatedClusterKind(string name, Props props, IMemberStrategy? strategy,
        CanSpawnIdentity? canSpawnIdentity)
    {
        Name = name;
        Props = props.WithClusterKind(this);
        Strategy = strategy;
        CanSpawnIdentity = canSpawnIdentity;
    }

    public string Name { get; }
    public Props Props { get; }
    public IMemberStrategy? Strategy { get; }
    public CanSpawnIdentity? CanSpawnIdentity { get; }

    internal long Count => Interlocked.Read(ref _count);

    internal long Inc() => Interlocked.Increment(ref _count);

    internal long Dec() => Interlocked.Decrement(ref _count);
}