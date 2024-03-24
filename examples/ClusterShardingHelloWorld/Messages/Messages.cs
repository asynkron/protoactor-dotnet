// -----------------------------------------------------------------------
// <copyright file = "Messages.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using Proto.Cluster.Sharding;

namespace ClusterHelloWorld.Messages;

public partial class HelloRequest : IShardMessage
{
    // proto message contains required property EntityId
}