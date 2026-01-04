// -----------------------------------------------------------------------
// <copyright file="KubernetesProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2025 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JetBrains.Annotations;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Proto.Diagnostics;
using Proto.Utils;
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes;

/// <summary>
///     Clustering provider that uses Kubernetes API to publish and discover members. Preferred provider
///     for Kubernetes deployments.
/// </summary>
[PublicAPI]
public class KubernetesProvider : BaseClusterProvider
{
    private static readonly ILogger _logger = Log.CreateLogger<KubernetesProvider>();
    private readonly KubernetesProviderConfig _config;

    private PID _clusterMonitor = null!;
    private string _podName = null!;

    protected override ILogger Logger => _logger;

    internal KubernetesProviderConfig Config => _config;

    public override async Task<DiagnosticsEntry[]> GetDiagnostics()
    {
        try
        {
            var selector = $"{LabelCluster}={_clusterName}";
            using var client = _config.ClientFactory();
            var res = await client.CoreV1.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesExtensions.GetKubeNamespace(),
                labelSelector: selector,
                timeoutSeconds: _config.WatchTimeoutSeconds,
                watch: false).ConfigureAwait(false);

            var pods = new DiagnosticsEntry("KubernetesProvider", "Pods", res.Body);

            return new[] { pods };
        }
        catch (Exception x)
        {
            return new[] { new DiagnosticsEntry("KubernetesProvider", "Exception", x.ToString()) };
        }
    }

    public KubernetesProvider() : this(new KubernetesProviderConfig())
    {
    }

    public KubernetesProvider(KubernetesProviderConfig config)
    {
        if (!KubernetesExtensions.TryGetKubeNamespace(out _))
        {
            throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");
        }

        _config = config;
    }

    [Obsolete("Do not pass a Kubernetes client directly, pass Client factory as part of Config, or use Config defaults",
        true)]
    public KubernetesProvider(IKubernetes kubernetes, KubernetesProviderConfig config)
    {
    }

    public override async Task StartMemberAsync(Cluster cluster)
    {
        await base.StartMemberAsync(cluster).ConfigureAwait(false);
        MonitorMemberStatusChanges();
    }

    public override Task StartClientAsync(Cluster cluster)
    {
        base.StartClientAsync(cluster);
        MonitorMemberStatusChanges();
        return Task.CompletedTask;
    }

    public override async Task ShutdownAsync(bool graceful)
    {
        await DeregisterMemberAsync(_cluster).ConfigureAwait(false);
        await _cluster.System.Root.StopAsync(_clusterMonitor).ConfigureAwait(false);
    }

    protected override async Task RegisterMemberInner()
    {
        var kubernetes = _config.ClientFactory();

        Logger.LogInformation("[Cluster][KubernetesProvider] Registering service {PodName} on {PodIp}", _podName,
            _address);

        var pod = await kubernetes.CoreV1.ReadNamespacedPodAsync(_podName, KubernetesExtensions.GetKubeNamespace()).ConfigureAwait(false);

        if (pod is null)
        {
            throw new ApplicationException($"Unable to get own pod information for {_podName}");
        }

        Logger.LogInformation("[Cluster][KubernetesProvider] Using Kubernetes namespace: {Namespace}", pod.Namespace());

        Logger.LogInformation("[Cluster][KubernetesProvider] Using Kubernetes port: {Port}", _port);

        var labels = new Dictionary<string, string>
        {
            [LabelCluster] = _clusterName,
            [LabelPort] = _port.ToString(),
            [LabelMemberId] = _cluster.System.Id,
        };

        AppendHostToPodLabels(pod, labels);

        foreach (var existing in pod.Metadata.Labels)
        {
            labels.TryAdd(existing.Key, existing.Value);
        }

        var annotations = new Dictionary<string, string>
        {
            [AnnotationKinds] = string.Join(';', _kinds),
        };

        if (pod.Metadata.Annotations is not null)
        {
            foreach (var existing in pod.Metadata.Annotations)
            {
                annotations.TryAdd(existing.Key, existing.Value);
            }
        }

        try
        {
            await kubernetes.ReplacePodLabelsAndAnnotations(_podName, KubernetesExtensions.GetKubeNamespace(), pod, labels, annotations).ConfigureAwait(false);
        }
        catch (Exception e)
        {
            Logger.LogError(e,
                "[Cluster][KubernetesProvider] Unable to update pod labels, registration failed. Labels : {Labels}",
                labels);

            throw;
        }
    }

    /// <summary>
    /// k8s labels have a limit of 63 characters, so we need to be careful about what we add to the labels.
    /// </summary>
    protected void AppendHostToPodLabels(V1Pod pod, Dictionary<string, string> labels)
    {
        // If our advertised host is something other then our pod IP, we need to add a label to the pod
        // So others can find us by our advertised host.
        var podId = pod.Status.PodIP;
        if (!_host.Equals(podId, StringComparison.OrdinalIgnoreCase))
        {
            if (_host.Length <= 63)
            {
                labels[LabelHost] = _host;
            }
            else
            {
                var dnsPostfix = $".{pod.Namespace()}.svc.{_config.ClusterDomain}";

                // If we have a subdomain, then we can add that to the dnsPostfix, as it will be known to the cluster
                if (!string.IsNullOrEmpty(pod.Spec.Subdomain))
                {
                    dnsPostfix = $".{pod.Spec.Subdomain}{dnsPostfix}";
                }

                if (_host.EndsWith(dnsPostfix))
                {
                    labels[LabelHostPrefix] = _host[..^dnsPostfix.Length];
                }
                else
                {
                    // TODO: what else could we do here?
                    // Going to fall back to the IP address, and hope that works...
                    Logger.LogWarning(
                        "[Cluster][KubernetesProvider] AdvertisedHost is too long to be used as a label, falling back to PodID, host: {Host}",
                        _host);
                }
            }
        }
    }

    protected override void StartClusterMonitor()
    {
        var props = Props
            .FromProducer(() => new KubernetesClusterMonitor(_cluster, _config))
            .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);

        _clusterMonitor = _cluster.System.Root.SpawnNamedSystem(props, "$kubernetes-cluster-monitor");
        _podName = KubernetesExtensions.GetPodName();

        _cluster.System.Root.Send(
            _clusterMonitor,
            new RegisterMember
            {
                ClusterName = _clusterName,
                Address = _address,
                Port = _port,
                Kinds = _kinds,
                MemberId = _cluster.System.Id
            }
        );
    }

    private async Task DeregisterMemberAsync(Cluster cluster)
    {
        var logger = Logger;
        await Retry.Try(() => DeregisterMemberInner(cluster), onError: OnError, onFailed: OnFailed).ConfigureAwait(false);

        void OnError(int attempt, Exception exception) =>
            logger.LogWarning(exception, "Failed to deregister service");

        void OnFailed(Exception exception) => logger.LogError(exception, "Failed to deregister service");
    }

    private async Task DeregisterMemberInner(Cluster cluster)
    {
        var kubernetes = _config.ClientFactory();

        Logger.LogInformation("[Cluster][KubernetesProvider] Unregistering service {PodName} on {PodIp}", _podName,
            _address);

        var kubeNamespace = KubernetesExtensions.GetKubeNamespace();

        var pod = await kubernetes.CoreV1.ReadNamespacedPodAsync(_podName, kubeNamespace).ConfigureAwait(false);

        var labels = pod.Metadata.Labels
            .Where(label => !label.Key.StartsWith(ProtoClusterPrefix, StringComparison.Ordinal))
            .ToDictionary(label => label.Key, label => label.Value);

        var annotations = pod.Metadata.Annotations
            .Where(label => !label.Key.StartsWith(ProtoClusterPrefix, StringComparison.Ordinal))
            .ToDictionary(label => label.Key, label => label.Value);

        await kubernetes.ReplacePodLabelsAndAnnotations(_podName, kubeNamespace, pod, labels, annotations).ConfigureAwait(false);

        cluster.System.Root.Send(_clusterMonitor, new DeregisterMember());
    }

    private void MonitorMemberStatusChanges() =>
        _cluster.System.Root.Send(_clusterMonitor, new StartWatchingCluster(_clusterName));
}
