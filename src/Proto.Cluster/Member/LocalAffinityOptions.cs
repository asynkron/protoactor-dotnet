// -----------------------------------------------------------------------
// <copyright file="LocalAffinityOptions.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using Proto.Utils;

namespace Proto.Cluster
{
    public sealed record LocalAffinityOptions
    {
        public ThrottleOptions? RelocationThroughput { get; init; }

        /// <summary>
        /// To prevent non-partitioned messages from triggering relocation of the virtual actor.
        /// If messages are sent from other nodes for which this predicate is true, the actor will be moved there.
        /// </summary>
        public Predicate<MessageEnvelope>? TriggersLocalAffinity { get; init; }
    }
}