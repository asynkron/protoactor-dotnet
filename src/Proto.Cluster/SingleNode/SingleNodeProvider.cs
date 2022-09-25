// -----------------------------------------------------------------------
// <copyright file = "SingleNodeProvider.cs" company = "Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;

namespace Proto.Cluster.SingleNode;

/// <summary>
///     Provides an in-memory cluster provider for a single node
///     Makes the cluster abstractions available for
///     single node scenarios, with zero need for external coordination
/// </summary>
public class SingleNodeProvider : IClusterProvider
{
    private Cluster? _cluster;

    public Task StartMemberAsync(Cluster cluster)
    {
        _cluster = cluster;
        var (host, port) = cluster.System.GetAddress();

        var member = new Member
        {
            Host = host,
            Port = port,
            Id = cluster.System.Id,
            Kinds = { cluster.GetClusterKinds() }
        };

        cluster.MemberList.UpdateClusterTopology(new[] { member });

        return Task.CompletedTask;
    }

    public Task StartClientAsync(Cluster cluster) =>
        throw new NotSupportedException("Single node provider does not support client mode");

    public Task ShutdownAsync(bool graceful)
    {
        _cluster?.MemberList.UpdateClusterTopology(Array.Empty<Member>());

        return Task.CompletedTask;
    }
}