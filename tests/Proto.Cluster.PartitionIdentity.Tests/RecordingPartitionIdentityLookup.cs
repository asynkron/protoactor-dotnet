// -----------------------------------------------------------------------
// <copyright file="RecordingPartitionIdentityLookup.cs" company="Asynkron AB">
//      Copyright (C) 2015-2024 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System.Threading;
using System.Threading.Tasks;
using Proto;
using Proto.Cluster.Identity;
using Proto.Cluster.Partition;
using Proto.Cluster.Tests;
using Proto.Diagnostics;

namespace Proto.Cluster.PartitionIdentity.Tests;

/// <summary>
/// Identity lookup wrapper that records activation requests before delegating
/// to the underlying <see cref="PartitionIdentityLookup"/>.
/// </summary>
public class RecordingPartitionIdentityLookup : IIdentityLookup
{
    private readonly PartitionIdentityLookup _inner;
    private readonly ActorStateRepo _repo;
    private readonly IClusterFixture _fixture;

    public RecordingPartitionIdentityLookup(PartitionIdentityLookup inner, ActorStateRepo repo, IClusterFixture fixture)
    {
        _inner = inner;
        _repo = repo;
        _fixture = fixture;
    }

    public Task<PID?> GetAsync(ClusterIdentity clusterIdentity, CancellationToken ct)
    {
        _repo.Get(clusterIdentity.Identity, _fixture).RecordActivationRequest();
        return _inner.GetAsync(clusterIdentity, ct);
    }

    public Task RemovePidAsync(ClusterIdentity clusterIdentity, PID pid, CancellationToken ct)
        => _inner.RemovePidAsync(clusterIdentity, pid, ct);

    public Task SetupAsync(Cluster cluster, string[] kinds, bool isClient)
        => _inner.SetupAsync(cluster, kinds, isClient);

    public Task ShutdownAsync() => _inner.ShutdownAsync();

    public Task<DiagnosticsEntry[]> GetDiagnostics()
        => (_inner as IDiagnosticsProvider).GetDiagnostics();
}
