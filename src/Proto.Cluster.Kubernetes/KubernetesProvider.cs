// -----------------------------------------------------------------------
// <copyright file="KubernetesProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Proto.Cluster.Data;
using Proto.Mailbox;
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes
{
    [PublicAPI]
    public class KubernetesProvider : IClusterProvider
    {
        private static readonly ILogger Logger = Log.CreateLogger<KubernetesProvider>();

        private readonly IKubernetes _kubernetes;
        private string _address;
        private Cluster _cluster;

        private PID _clusterMonitor;
        private string _clusterName;
        private string _host;
        private string[] _kinds;
        private MemberList _memberList;
        private string _podName;
        private int _port;

        public KubernetesProvider(IKubernetes kubernetes)
        {
            if (KubernetesExtensions.GetKubeNamespace() is null)
                throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");

            _kubernetes = kubernetes;
        }

        public async Task StartMemberAsync(Cluster cluster)
        {
            var memberList = cluster.MemberList;
            var clusterName = cluster.Config.ClusterName;
            var (host, port) = cluster.System.GetAddress();
            var kinds = cluster.GetClusterKinds();
            _cluster = cluster;
            _memberList = memberList;
            _clusterName = clusterName;
            _host = host;
            _port = port;
            _kinds = kinds;
            _address = host + ":" + port;
            StartClusterMonitor();
            await RegisterMemberAsync();
            MonitorMemberStatusChanges();
        }

        public Task StartClientAsync(Cluster cluster)
        {
            var memberList = cluster.MemberList;
            var clusterName = cluster.Config.ClusterName;
            var (host, port) = cluster.System.GetAddress();
            _cluster = cluster;
            _memberList = memberList;
            _clusterName = clusterName;
            _host = host;
            _port = port;
            _kinds = Array.Empty<string>();
            StartClusterMonitor();
            MonitorMemberStatusChanges();
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(bool graceful)
        {
            await DeregisterMemberAsync(_cluster);
            await _cluster.System.Root.StopAsync(_clusterMonitor);
        }

        public async Task RegisterMemberAsync()
        {
            Logger.LogInformation("Registering service {PodName} on {PodIp}", _podName, _address);

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, KubernetesExtensions.GetKubeNamespace());
            if (pod is null) throw new ApplicationException($"Unable to get own pod information for {_podName}");

            Logger.LogInformation("Using Kubernetes namespace: " + pod.Namespace());

            var matchingPort = pod.FindPort(_port);

            if (matchingPort is null) Logger.LogWarning("Registration port doesn't match any of the container ports");

            Logger.LogInformation("Using Kubernetes port: " + _port);

            var existingLabels = pod.Metadata.Labels;

            var labels = new Dictionary<string, string>
            {
                [LabelCluster] = _clusterName,
                [LabelPort] = _port.ToString(),
                [LabelMemberId] = _cluster.System.Id
            };

            foreach (var kind in _kinds)
            {
                var labelKey = $"{LabelKind}-{kind}";
                labels.TryAdd(labelKey, "true");
            }

            //add existing labels back
            foreach (var existing in existingLabels)
            {
                labels.TryAdd(existing.Key, existing.Value);
            }

            try
            {
                await _kubernetes.ReplacePodLabels(_podName, KubernetesExtensions.GetKubeNamespace(), labels);
            }
            catch (HttpOperationException e)
            {
                Logger.LogError(e, "Unable to update pod labels, registration failed");
                throw;
            }
        }

        private void StartClusterMonitor()
        {
            var props = Props
                .FromProducer(() => new KubernetesClusterMonitor(_cluster, _kubernetes))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Dispatchers.SynchronousDispatcher);
            _clusterMonitor = _cluster.System.Root.SpawnNamed(props, "ClusterMonitor");
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

        public async Task DeregisterMemberAsync(Cluster cluster)
        {
            Logger.LogInformation("Unregistering service {PodName} on {PodIp}", _podName, _address);

            var kubeNamespace = KubernetesExtensions.GetKubeNamespace();

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, kubeNamespace);

            foreach (var kind in _kinds)
            {
                try
                {
                    var labelKey = $"{LabelKind}-{kind}";
                    pod.SetLabel(labelKey, null);
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "Failed to remove label");
                }
            }

            pod.SetLabel(LabelCluster, null);

            await _kubernetes.ReplacePodLabels(_podName, kubeNamespace, pod.Labels());

            cluster.System.Root.Send(_clusterMonitor, new DeregisterMember());
        }

        public void MonitorMemberStatusChanges() => _cluster.System.Root.Send(_clusterMonitor, new StartWatchingCluster(_clusterName));
    }
}