// -----------------------------------------------------------------------
// <copyright file = "SeedNodeClusterProviderOptions.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Proto.Cluster.Seed;

public record SeedNodeClusterProviderOptions(ISeedNodeDiscovery Discovery);