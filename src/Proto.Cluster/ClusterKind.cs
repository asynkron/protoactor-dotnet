// -----------------------------------------------------------------------
// <copyright file="ClusterKind.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;

namespace Proto.Cluster;

/// <summary>
///     A delegate to check if certain identity is in the set of allowed identities.
/// </summary>
public delegate ValueTask<bool> CanSpawnIdentity(string identity, CancellationToken cancellationToken);

/// <summary>
///     Cluster kind defines a type of virtual actor that can be spawned in the cluster
/// </summary>
/// <param name="Name">Virtual actor type name</param>
/// <param name="Props">Props to spawn the virtual actor</param>
[PublicAPI]
public record ClusterKind(string Name, Props Props)
{
    /// <summary>
    ///     <see cref="IMemberStrategy" /> to be used when placing the actor in the cluster
    /// </summary>
    [JsonIgnore] public Func<Cluster, IMemberStrategy>? StrategyBuilder { get; init; }

    /// <summary>
    ///     Optional filter that can prevent spawning identities outside of a certain set of allowed identities.
    ///     Useful e.g. in cases where actors are spawned as part of REST API call where actor identity is provided by the API
    ///     user.
    ///     In this case the system could be protected from DoS attacks by preventing spawning random identities.
    /// </summary>
    [JsonIgnore] public CanSpawnIdentity? CanSpawnIdentity { get; init; }

    
    /// <summary>Props to spawn the virtual actor</summary>
    [JsonIgnore] public Props Props { get; init; } = Props;

    /// <summary>
    ///     Creates a copy of the ClusterKind with the updated Props
    /// </summary>
    /// <param name="configureProps">Function to configure a new Props</param>
    /// <returns></returns>
    public ClusterKind WithProps(Func<Props, Props> configureProps) => 
        this with { Props = configureProps(Props) };

    /// <summary>
    ///     Sets the <see cref="IMemberStrategy" /> to be used when placing the actor in the cluster
    /// </summary>
    /// <param name="strategyBuilder"></param>
    /// <returns></returns>
    public ClusterKind WithMemberStrategy(Func<Cluster, IMemberStrategy> strategyBuilder) =>
        this with { StrategyBuilder = strategyBuilder };

    /// <summary>
    ///     Sets the optional filter that can prevent spawning identities outside of a certain set of allowed identities.
    ///     Useful e.g. in cases where actors are spawned as part of REST API call where actor identity is provided by the API
    ///     user.
    ///     In this case the system could be protected from DoS attacks by preventing spawning random identities.
    /// </summary>
    /// <param name="spawnPredicate"></param>
    /// <returns></returns>
    public ClusterKind WithSpawnPredicate(CanSpawnIdentity spawnPredicate) =>
        this with { CanSpawnIdentity = spawnPredicate };

    internal ActivatedClusterKind Build(Cluster cluster) =>
        new(Name, Props, StrategyBuilder?.Invoke(cluster), CanSpawnIdentity);
}