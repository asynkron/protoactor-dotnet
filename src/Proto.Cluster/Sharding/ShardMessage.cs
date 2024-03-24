// -----------------------------------------------------------------------
// <copyright file = "ShardMessage.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Sharding;

public interface IShardMessage
{
    string EntityId { get; }
}

