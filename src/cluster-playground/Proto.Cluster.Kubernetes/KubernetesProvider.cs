// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
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
        private string[] _kinds;
        private string _podName;
        private int _port;
        private MemberList _memberList;

        public KubernetesProvider(IKubernetes kubernetes)
        {
            if (KubernetesExtensions.GetKubeNamespace() == null)
                throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");

            _kubernetes = kubernetes;
        }

        public async Task StartMemberAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            MemberList memberList)
        {
            _cluster = cluster;
            _memberList = memberList;

            await RegisterMemberAsync(cluster, clusterName, host, port, kinds);
            MonitorMemberStatusChanges(_cluster);
        }

        public Task StartClientAsync(Cluster cluster, string clusterName, string host, int port, MemberList memberList)
        {
            _cluster = cluster;
            _memberList = memberList;
            MonitorMemberStatusChanges(_cluster);
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(bool graceful)
        {
            await DeregisterMemberAsync(_cluster);
            await _cluster.System.Root.StopAsync(_clusterMonitor);
        }

        public Task UpdateClusterState(ClusterState state)
        {
            return Task.CompletedTask;
        }

        public async Task RegisterMemberAsync(
            Cluster cluster,
            string clusterName, string address, int port, string[] kinds
        )
        {
            if (string.IsNullOrEmpty(clusterName)) throw new ArgumentNullException(nameof(clusterName));

            var props = Props
                .FromProducer(() => new KubernetesClusterMonitor(cluster.System, _kubernetes))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Dispatchers.SynchronousDispatcher);
            _clusterMonitor = cluster.System.Root.SpawnNamed(props, "ClusterMonitor");
            _clusterName = clusterName;
            _address = address;
            _port = port;
            _kinds = kinds;
            _podName = KubernetesExtensions.GetPodName();

            Logger.LogInformation("Registering service {PodName} on {PodIp}", _podName, _address);

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, KubernetesExtensions.GetKubeNamespace());

            if (pod == null) throw new ApplicationException($"Unable to get own pod information for {_podName}");

            var matchingPort = pod.FindPort(_port);

            if (matchingPort == null) Logger.LogWarning("Registration port doesn't match any of the container ports");

            var protoKinds = new List<string>();

            protoKinds.AddRange(_kinds);

            var labels = new Dictionary<string, string>(pod.Metadata.Labels)
            {
                [LabelCluster] = _clusterName,
                [LabelKinds] = string.Join(",", protoKinds.Distinct()),
                [LabelPort] = _port.ToString()
            };

            try
            {
                await _kubernetes.ReplacePodLabels(_podName, KubernetesExtensions.GetKubeNamespace(), labels);
            }
            catch (HttpOperationException e)
            {
                Logger.LogError(e, "Unable to update pod labels, registration failed");
                throw;
            }

            cluster.System.Root.Send(
                _clusterMonitor,
                new RegisterMember
                {
                    ClusterName = clusterName,
                    Address = address,
                    Port = port,
                    Kinds = kinds
                }
            );
        }

        public async Task DeregisterMemberAsync(Cluster cluster)
        {
            Logger.LogInformation("Unregistering service {PodName} on {PodIp}", _podName, _address);

            var kubeNamespace = KubernetesExtensions.GetKubeNamespace();

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, kubeNamespace);
            pod.SetLabel(LabelKinds, null);
            pod.SetLabel(LabelCluster, null);
            await _kubernetes.ReplacePodLabels(_podName, kubeNamespace, pod.Labels());

            cluster.System.Root.Send(_clusterMonitor, new DeregisterMember());
        }


        public void MonitorMemberStatusChanges(Cluster cluster)
        {
            cluster.System.Root.Send(_clusterMonitor, new StartWatchingCluster(_clusterName));
        }
    }
}