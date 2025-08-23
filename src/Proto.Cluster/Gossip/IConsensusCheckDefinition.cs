// -----------------------------------------------------------------------
// <copyright file="IConsensusCheck.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Collections.Immutable;

namespace Proto.Cluster.Gossip;

/// <summary>
///     Definition of a gossip consensus check used by <see cref="Gossiper.RegisterConsensusCheck{T}(IConsensusCheckDefinition{T})"/>.
/// </summary>
/// <typeparam name="T">Type of the value that should be in agreement across members.</typeparam>
/// <example>
/// <code>
/// var definition = new Gossiper.ConsensusCheckBuilder&lt;int&gt;("config", any => any.Unpack&lt;Int32Value&gt;().Value);
/// </code>
/// </example>
public interface IConsensusCheckDefinition<T> where T : notnull
{
    public ConsensusCheck<T> Check { get; }
    public IImmutableSet<string> AffectedKeys { get; }
}
