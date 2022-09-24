// -----------------------------------------------------------------------
// <copyright file="LocalAffinityOptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using Proto.Utils;

namespace Proto.Cluster;

public sealed record LocalAffinityOptions
{
    /// <summary>
    ///     Throttle the number of relocations in a window of time to avoid slowdowns in the system.
    /// </summary>
    public ThrottleOptions? RelocationThroughput { get; init; }

    /// <summary>
    ///     A predicate that should return true for messages that trigger local affinity mechanism. If you also
    ///     have non-partitioned messages sent to an actor, return false for those messages.
    /// </summary>
    /// <remarks>When using code generated grains, this predicate receives <see cref="GrainRequestMessage" /> in the envelope</remarks>
    public Predicate<MessageEnvelope>? TriggersLocalAffinity { get; init; }
}