// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProviderOptions.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Proto.Cluster.Seed;

public record SeedNodeClusterProviderOptions
{
    public SeedNodeClusterProviderOptions(params (string, int)[] seeds)
    {
        SeedNodes = seeds.ToImmutableList();
    }

    public ImmutableList<(string host, int port)> SeedNodes { get; init; } =
        ImmutableList<(string host, int port)>.Empty;
}