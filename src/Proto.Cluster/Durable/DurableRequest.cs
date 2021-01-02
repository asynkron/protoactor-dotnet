// -----------------------------------------------------------------------
// <copyright file="DurableRequest.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
namespace Proto.Cluster.Durable
{
    public record DurableRequest(ClusterIdentity Sender, ClusterIdentity Target, object Message, int Id);
}