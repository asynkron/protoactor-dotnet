// -----------------------------------------------------------------------
// <copyright file="PartitionConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Partition
{
    public record PartitionConfig
    {
        public TimeSpan GetPidTimeout { get; init; } = TimeSpan.FromSeconds(5);
        public int HandoverChunkSize { get; init; } = 5000;

        /// <summary>
        /// This is the longest the system will wait for the current activations to complete before forcing a rebalance.
        /// </summary>
        public TimeSpan RebalanceActivationsCompletionTimeout { get; init; } = TimeSpan.FromSeconds(10);
        public TimeSpan RebalanceRequestTimeout { get; init; } = TimeSpan.FromSeconds(2);
        
        /// <summary>
        /// Determines which side initiates the identity handover.
        /// Pull is stable, Push is currently experimental </summary>
        public PartitionIdentityLookup.Mode Mode { get; init; } = PartitionIdentityLookup.Mode.Pull;
        
        /// <summary>
        /// Determines if all activations are sent or only ones that changed owner from the previous topology
        /// Delta is currently experimental.
        /// </summary>
        public PartitionIdentityLookup.Send Send { get; init; } = PartitionIdentityLookup.Send.Full;
        public bool DeveloperLogging { get; init; } = false;
    }
}