// -----------------------------------------------------------------------
// <copyright file="IConsensusCheck.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Proto.Cluster.Gossip;

public interface IConsensusCheckDefinition<T> where T : notnull
{
    public ConsensusCheck<T> Check { get; }
    public IImmutableSet<string> AffectedKeys { get; }
}