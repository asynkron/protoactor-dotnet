﻿using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.ResourceManager;
using Azure.ResourceManager.AppContainers;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proto.Cluster.AzureContainerApps.Stores.ResourceTags;
using Proto.Utils;

namespace Proto.Cluster.AzureContainerApps;

[PublicAPI]
public class AzureContainerAppsProvider : IClusterProvider
{
    private readonly ArmClient _client;
    private readonly IMemberStore _memberStore;
    private readonly IOptions<AzureContainerAppsProviderOptions> _options;
    private readonly ILogger _logger;

    private string _memberId = null!;
    private string _address = null!;
    private Cluster _cluster = null!;
    private string _clusterName = null!;
    private string[] _kinds = null!;
    private int _port;

    /// <summary>
    /// Use this constructor to create a new instance.
    /// </summary>
    /// <param name="client">An existing <see cref="ArmClient"/></param> instance that you need to bring yourself.
    /// <param name="memberStore">The store to use for storing member information.</param>
    /// <param name="options">The options for this provider.</param>
    /// <param name="logger">The logger to use.</param>
    public AzureContainerAppsProvider(
        ArmClient client,
        IMemberStore memberStore,
        IOptions<AzureContainerAppsProviderOptions> options,
        ILogger<AzureContainerAppsProvider> logger)
    {
        _client = client;
        _memberStore = memberStore;
        _options = options;
        _logger = logger;
    }

    public async Task StartMemberAsync(Cluster cluster)
    {
        var clusterName = cluster.Config.ClusterName;
        var (host, port) = cluster.System.GetAddress();
        var kinds = cluster.GetClusterKinds();
        _cluster = cluster;
        _clusterName = clusterName;
        _memberId = cluster.System.Id;
        _port = port;
        _kinds = kinds;
        _address = $"{host}:{port}";

        await RegisterMemberAsync().ConfigureAwait(false);
        StartClusterMonitor();
    }

    public Task StartClientAsync(Cluster cluster)
    {
        var clusterName = cluster.Config.ClusterName;
        var (_, port) = cluster.System.GetAddress();
        _cluster = cluster;
        _clusterName = clusterName;
        _memberId = cluster.System.Id;
        _port = port;
        _kinds = Array.Empty<string>();

        StartClusterMonitor();
        return Task.CompletedTask;
    }

    public async Task ShutdownAsync(bool graceful) => await DeregisterMemberAsync().ConfigureAwait(false);

    private async Task RegisterMemberAsync()
    {
        await Retry.Try(RegisterMemberInner, retryCount: Retry.Forever, onError: OnError, onFailed: OnFailed).ConfigureAwait(false);

        void OnError(int attempt, Exception exception) => _logger.LogWarning(exception, "Failed to register service");
        void OnFailed(Exception exception) => _logger.LogError(exception, "Failed to register service");
    }

    private async Task RegisterMemberInner()
    {
        var resourceGroupName = _options.Value.ResourceGroupName;
        var containerAppName = _options.Value.ContainerAppName;
        var revisionName = _options.Value.RevisionName;
        var resourceGroup = await _client.GetResourceGroupByName(resourceGroupName).ConfigureAwait(false);
        var containerApp = await resourceGroup.Value.GetContainerAppAsync(containerAppName).ConfigureAwait(false);
        var revision = await containerApp.Value.GetContainerAppRevisionAsync(revisionName).ConfigureAwait(false);

        if ((revision.Value.Data.TrafficWeight ?? 0) == 0)
            return;

        var replicaName = _options.Value.ReplicaName;
        var advertisedHost = _options.Value.AdvertisedHost ?? _address;

        var member = new Member
        {
            Id = _memberId,
            Host = advertisedHost,
            Port = _port,
        };

        _logger.LogInformation(
            "[Cluster][AzureContainerAppsProvider] Registering service {ReplicaName} on {IpAddress}",
            replicaName,
            _address);

        member.Kinds.AddRange(_kinds);
        await _memberStore.RegisterAsync(_clusterName, member).ConfigureAwait(false);
    }

    private void StartClusterMonitor()
    {
        var pollInterval = _options.Value.PollInterval;
        var storeName = _memberStore.GetType().Name;

        _ = SafeTask.Run(async () =>
            {
                while (!_cluster.System.Shutdown.IsCancellationRequested)
                {
                    _logger.LogInformation("Looking for members in {Store}", storeName);

                    try
                    {
                        var members = (await _memberStore.ListAsync().ConfigureAwait(false)).ToArray();

                        if (members.Any())
                        {
                            _logger.LogInformation("Got members {Members}", members.Length);
                            _cluster.MemberList.UpdateClusterTopology(members);
                        }
                        else
                        {
                            _logger.LogWarning("Failed to get members from {Store}", storeName);
                        }
                    }
                    catch (Exception x)
                    {
                        _logger.LogError(x, "Failed to get members from {Store}", storeName);
                    }

                    await Task.Delay(pollInterval).ConfigureAwait(false);
                }
            }
        );
    }

    private async Task DeregisterMemberAsync()
    {
        await Retry.Try(DeregisterMemberInner, onError: OnError, onFailed: OnFailed).ConfigureAwait(false);
        void OnError(int attempt, Exception exception) => _logger.LogWarning(exception, "Failed to deregister service");
        void OnFailed(Exception exception) => _logger.LogError(exception, "Failed to deregister service");
    }

    private async Task DeregisterMemberInner()
    {
        var replicaName = _options.Value.ReplicaName;

        _logger.LogInformation(
            "[Cluster][AzureContainerAppsProvider] Unregistering member {ReplicaName} on {IpAddress}",
            replicaName,
            _address);

        await _memberStore.UnregisterAsync(_memberId).ConfigureAwait(false);
    }
}