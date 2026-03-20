// -----------------------------------------------------------------------
// <copyright file="AmazonEcsProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.ECS;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.AmazonECS;

[PublicAPI]
public class AmazonEcsProvider : BaseClusterProvider
{
    private static readonly ILogger _logger = Log.CreateLogger<AmazonEcsProvider>();
    private readonly IAmazonECS _client;
    private readonly AmazonEcsProviderConfig _config;
    private readonly string _ecsClusterName;
    private readonly string _taskArn;
    private readonly int? _advertisedPort;
    private readonly string _advertisedHost;

    public AmazonEcsProvider(IAmazonECS client, string ecsClusterName, string taskArn, AmazonEcsProviderConfig config, int? advertisedPort, string advertisedHost)
    {
        _ecsClusterName = ecsClusterName;
        _client = client;
        _config = config;
        _taskArn = taskArn;
        _advertisedPort = advertisedPort;
        _advertisedHost = advertisedHost;
    }

    protected override ILogger Logger => _logger;

    public override async Task ShutdownAsync(bool graceful) => await DeregisterMemberAsync().ConfigureAwait(false);

    protected override async Task RegisterMemberInner()
    {
        Logger.LogInformation("[Cluster][AmazonEcsProvider] Registering service {PodName} on {PodIp}", _taskArn,
            _address);

        var tags = new Dictionary<string, string>
        {
            [ProtoLabels.LabelCluster] = _clusterName,
            [ProtoLabels.LabelPort] = (_advertisedPort ?? _port).ToString(),
            [ProtoLabels.LabelMemberId] = _cluster.System.Id,
        };

        if (!string.IsNullOrEmpty(_advertisedHost))
        {
            tags[ProtoLabels.LabelHost] = _advertisedHost;
        }

        foreach (var kind in _kinds)
        {
            var labelKey = $"{ProtoLabels.LabelKind}-{kind}";
            tags.TryAdd(labelKey, "true");
        }

        try
        {
            await _client.UpdateMetadata(_taskArn, tags).ConfigureAwait(false);
        }
        catch (Exception x)
        {
            Logger.LogError(x, "Failed to update metadata");
        }
    }

    protected override void StartClusterMonitor() =>
        _ = SafeTask.Run(async () =>
            {
                while (!_cluster.System.Shutdown.IsCancellationRequested)
                {
                    Logger.Log(_config.DebugLogLevel, "Calling ECS API");

                    try
                    {
                        var members = await _client.GetMembers(_ecsClusterName).ConfigureAwait(false);

                        if (members != null)
                        {
                            Logger.Log(_config.DebugLogLevel, "Got members {Members}", members.Length);
                            _cluster.MemberList.UpdateClusterTopology(members);
                        }
                        else
                        {
                            Logger.LogWarning("Failed to get members from ECS");
                        }
                    }
                    catch (Exception x)
                    {
                        Logger.LogError(x, "Failed to get members from ECS");
                    }

                    // Wait before polling ECS again for cluster membership changes
                    await Task.Delay(TimeSpan.FromSeconds(_config.PollIntervalSeconds)).ConfigureAwait(false);
                }
            }
        );

    private async Task DeregisterMemberAsync()
    {
        var logger = Logger;
        await Retry.Try(DeregisterMemberInner, onError: OnError, onFailed: OnFailed).ConfigureAwait(false);

        void OnError(int attempt, Exception exception) =>
            logger.LogWarning(exception, "Failed to deregister service");

        void OnFailed(Exception exception) => logger.LogError(exception, "Failed to deregister service");
    }

    private async Task DeregisterMemberInner()
    {
        Logger.LogInformation("[Cluster][AmazonEcsProvider] Unregistering service {PodName} on {PodIp}", _taskArn,
            _address);

        await _client.ClearMetadata(_taskArn).ConfigureAwait(false);
    }
}
