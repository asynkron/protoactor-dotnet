// -----------------------------------------------------------------------
// <copyright file="ClusterState.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Data
{
    public class ClusterState
    {
        public string[] BannedMembers { get; set; } = null!;
    }
}