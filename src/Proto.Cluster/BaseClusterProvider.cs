// -----------------------------------------------------------------------
// <copyright file="BaseClusterProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;
using Proto.Utils;

namespace Proto.Cluster;

/// <summary>
///     Base class for cluster providers that share common initialization logic.
/// </summary>
[PublicAPI]
public abstract class BaseClusterProvider : IClusterProvider
{
    protected Cluster _cluster = null!;
    protected MemberList _memberList = null!;
    protected string _clusterName = null!;
    protected string _host = null!;
    protected int _port;
    protected string[] _kinds = null!;
    protected string _address = null!;

    /// <summary>
    ///     Gets the logger for this provider.
    /// </summary>
    protected abstract ILogger Logger { get; }

    public virtual Task<DiagnosticsEntry[]> GetDiagnostics() => Task.FromResult(Array.Empty<DiagnosticsEntry>());

    public virtual async Task StartMemberAsync(Cluster cluster)
    {
        InitializeClusterInfo(cluster, isMember: true);
        StartClusterMonitor();
        await RegisterMemberAsync().ConfigureAwait(false);
    }

    public virtual Task StartClientAsync(Cluster cluster)
    {
        InitializeClusterInfo(cluster, isMember: false);
        StartClusterMonitor();
        return Task.CompletedTask;
    }

    public abstract Task ShutdownAsync(bool graceful);

    protected virtual void InitializeClusterInfo(Cluster cluster, bool isMember)
    {
        var memberList = cluster.MemberList;
        var clusterName = cluster.Config.ClusterName;
        var (host, port) = cluster.System.GetAddress();

        _cluster = cluster;
        _memberList = memberList;
        _clusterName = clusterName;
        _host = host;
        _port = port;
        _kinds = isMember ? cluster.GetClusterKinds() : Array.Empty<string>();
        _address = $"{host}:{port}";
    }

    protected abstract void StartClusterMonitor();

    protected virtual async Task RegisterMemberAsync()
    {
        var logger = Logger;
        await Retry.Try(RegisterMemberInner, onError: OnError, onFailed: OnFailed, retryCount: Retry.Forever).ConfigureAwait(false);

        void OnError(int attempt, Exception exception) =>
            logger.LogWarning(exception, "Failed to register service");

        void OnFailed(Exception exception) => logger.LogError(exception, "Failed to register service");
    }

    protected abstract Task RegisterMemberInner();
}
