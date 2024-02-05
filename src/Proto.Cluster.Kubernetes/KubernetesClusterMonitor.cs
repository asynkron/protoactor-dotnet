// -----------------------------------------------------------------------
// <copyright file="KubernetesClusterMonitor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2022 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Autorest;
using k8s.Models;
using Microsoft.Extensions.Logging;
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes;

internal class KubernetesClusterMonitor : IActor
{
    private static readonly ILogger Logger = Log.CreateLogger<KubernetesClusterMonitor>();
    private readonly Cluster _cluster;

    private readonly Dictionary<string, V1Pod> _clusterPods = new();
    private readonly KubernetesProviderConfig _config;

    private string _address;
    private string _clusterName;
    private IKubernetes _kubernetes;
    private string _podName;
    private bool _stopping;
    private Watcher<V1Pod> _watcher;
    private Task<HttpOperationResponse<V1PodList>> _watcherTask;
    private bool _watching;

    public KubernetesClusterMonitor(Cluster cluster, KubernetesProviderConfig config)
    {
        _cluster = cluster;
        _kubernetes = config.ClientFactory();
        _config = config;
    }

    public Task ReceiveAsync(IContext context) =>
        context.Message switch
        {
            RegisterMember cmd => Register(cmd),
            StartWatchingCluster _ => StartWatchingCluster(context),
            DeregisterMember => StopWatchingCluster(),
            Stopping => StopWatchingCluster(),
            _ => Task.CompletedTask
        };

    private Task Register(RegisterMember cmd)
    {
        _clusterName = cmd.ClusterName;
        _address = cmd.Address;
        _podName = KubernetesExtensions.GetPodName();

        return Task.CompletedTask;
    }

    private Task StopWatchingCluster()
    {
        // ReSharper disable once InvertIf
        if (_watching)
        {
            Logger.LogInformation(
                "[Cluster][KubernetesProvider] Stopping monitoring for {PodName} with ip {PodIp}",
                _podName, _address
            );

            _stopping = true;

            DisposeWatcher();
            DisposeWatcherTask();
        }

        return Task.CompletedTask;
    }

    private Task StartWatchingCluster(IContext context)
    {
        _ = SafeTask.Run(async () =>
        {
            try
            {
                await Poll();
            }
            catch (Exception x)
            {
                Logger.LogError(x, "[Cluster][KubernetesProvider] Failed to poll the Kubernetes API");
            }

            if (!_config.DisableWatch)
            {
                await Watch();
            }

            await Task.Delay(1000);

            context.Send(context.Self, new StartWatchingCluster(_clusterName));
        });

        return Task.CompletedTask;
    }

    private Task Watch()
    {
        var tcs = new TaskCompletionSource();

        _watcherTask = GetListTask(_clusterName, true, _config.WatchTimeoutSeconds);
        _watcher = _watcherTask.Watch<V1Pod, V1PodList>(Watch, Error, Closed);
        _watching = true;

        void Error(Exception ex)
        {
            // If we are already in stopping state, just ignore it
            if (_stopping)
            {
                return;
            }

            // We log it and attempt to watch again, overcome transient issues
            Logger.LogError(ex, "[Cluster][KubernetesProvider] Unable to watch the cluster status");

            RecreateKubernetesClient();
            Restart();
        }

        // The watcher closes from time to time and needs to be restarted
        void Closed()
        {
            // If we are already in stopping state, just ignore it
            if (_stopping)
            {
                return;
            }

            Logger.Log(_config.DebugLogLevel, "[Cluster][KubernetesProvider] Watcher has closed, restarting");
            Restart();
        }

        void Restart()
        {
            _watching = false;

            DisposeWatcher();
            DisposeWatcherTask();

            tcs.SetResult();
        }

        return tcs.Task;
    }

    private async Task Poll()
    {
        var x = await GetListTask(_clusterName, false, 2);
        foreach (var eventPod in x.Body.Items)
        {
            var podLabels = eventPod.Metadata.Labels;

            if (!podLabels.TryGetValue(LabelCluster, out var podClusterName))
            {
                Logger.LogInformation(
                    "[Cluster][KubernetesProvider] The pod {PodName} is not a Proto.Cluster node",
                    eventPod.Metadata.Name
                );

                continue;
            }

            if (_clusterName != podClusterName)
            {
                Logger.LogInformation(
                    "[Cluster][KubernetesProvider] The pod {PodName} is from another cluster {Cluster}",
                    eventPod.Metadata.Name, _clusterName
                );

                continue;
            }

            _clusterPods[eventPod.Uid()] = eventPod;
        }

        var uids = x.Body.Items.Select(p => p.Uid()).ToHashSet();
        var toRemove = _clusterPods.Keys.Where(k => !uids.Contains(k)).ToList();

        foreach (var uid in toRemove)
        {
            _clusterPods.Remove(uid);
        }

        UpdateTopology();
    }

    private Task<HttpOperationResponse<V1PodList>> GetListTask(string clusterName, bool watch, int timeoutInSeconds)
    {
        var selector = $"{LabelCluster}={clusterName}";

        Logger.Log(_config.DebugLogLevel, "[Cluster][KubernetesProvider] Starting to watch pods with {Selector}",
            selector);

        return _kubernetes.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
            KubernetesExtensions.GetKubeNamespace(),
            labelSelector: selector,
            watch: watch,
            timeoutSeconds: timeoutInSeconds
        );
    }

    private void RecreateKubernetesClient()
    {
        DisposeWatcher();
        DisposeWatcherTask();
        DisposeKubernetesClient();

        Logger.LogWarning("[Cluster][KubernetesProvider] Recreating Kubernetes client due to connectivity error");
        _kubernetes = _config.ClientFactory();
    }

    private void DisposeKubernetesClient()
    {
        try
        {
            _kubernetes.Dispose();
        }
        catch
        {
            //pass
        }
    }

    private void DisposeWatcherTask()
    {
        try
        {
            _watcherTask?.Dispose();
        }
        catch
        {
            //pass
        }
    }

    private void DisposeWatcher()
    {
        try
        {
            _watcher?.Dispose();
        }
        catch
        {
            //pass
        }
    }

    private void Watch(WatchEventType eventType, V1Pod eventPod)
    {
        var podLabels = eventPod.Metadata.Labels;

        if (!podLabels.TryGetValue(LabelCluster, out var podClusterName))
        {
            Logger.LogInformation(
                "[Cluster][KubernetesProvider] The pod {PodName} is not a Proto.Cluster node",
                eventPod.Metadata.Name
            );

            return;
        }

        if (_clusterName != podClusterName)
        {
            Logger.LogInformation(
                "[Cluster][KubernetesProvider] The pod {PodName} is from another cluster {Cluster}",
                eventPod.Metadata.Name, _clusterName
            );

            return;
        }

        // Update the list of known pods
        if (eventType == WatchEventType.Deleted)
        {
            _clusterPods.Remove(eventPod.Uid());
        }
        else
        {
            _clusterPods[eventPod.Uid()] = eventPod;
        }

        UpdateTopology();
    }

    private void UpdateTopology()
    {
        if (_clusterPods?.Values is null)
        {
            Logger.LogInformation("[Cluster][KubernetesProvider] No pods found in the cluster");
            return;
        }
        
        var memberStatuses = _clusterPods.Values
            .Select(x => x.GetMemberStatus(_config))
            .Where(x => x is not null)
            .Where(x => x.IsRunning && (x.IsReady || x.Member.Id == _cluster.System.Id))
            .Select(x => x.Member)
            .ToList();

        Logger.Log(_config.DebugLogLevel, "[Cluster][KubernetesProvider] Topology received from Kubernetes {Members}",
            memberStatuses);

        try
        {
            _cluster.MemberList.UpdateClusterTopology(memberStatuses);
        }
        catch (Exception x)
        {
            Logger.LogError(x, "[Cluster][KubernetesProvider] Error updating MemberList with members data {Members}",
                memberStatuses);

            throw;
        }
    }
}