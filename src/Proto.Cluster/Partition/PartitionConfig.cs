// -----------------------------------------------------------------------
// <copyright file="PartitionConfig.cs" company="Asynkron AB">
//      Copyright (C) 2015-2021 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;

namespace Proto.Cluster.Partition
{
    public record PartitionConfig(bool DeveloperLogging, int HandoverChunkSize, TimeSpan RebalanceRequestTimeout);
}