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
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes
{
    [PublicAPI]
    public class KubernetesProvider : IClusterProvider
    {
        static readonly ILogger Logger = Log.CreateLogger<KubernetesProvider>();

        readonly IKubernetes _kubernetes;

        public KubernetesProvider(IKubernetes kubernetes)
        {
            if (KubernetesExtensions.GetKubeNamespace() == null)
            {
                throw new InvalidOperationException("The application doesn't seem to be running in Kubernetes");
            }

            _kubernetes = kubernetes;
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
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);
            _clusterMonitor        = cluster.System.Root.SpawnNamed(props, "ClusterMonitor");
            _clusterName           = clusterName;
            _address               = address;
            _port                  = port;
            _kinds                 = kinds;
            _podName               = KubernetesExtensions.GetPodName();

            Logger.LogInformation("Registering service {PodName} on {PodIp}", _podName, _address);

            var pod = await _kubernetes.ReadNamespacedPodAsync(_podName, KubernetesExtensions.GetKubeNamespace());

            if (pod == null) throw new ApplicationException($"Unable to get own pod information for {_podName}");

            var matchingPort = pod.FindPort(_port);

            if (matchingPort == null)
            {
                Logger.LogWarning("Registration port doesn't match any of the container ports");
            }

            var protoKinds = new List<string>();

            if (pod.Metadata.Labels.TryGetValue(LabelKinds, out var protoKindsString))
            {
                protoKinds.AddRange(protoKindsString.Split(','));
            }

            protoKinds.AddRange(_kinds);

            var labels = new Dictionary<string, string>(pod.Metadata.Labels)
            {
                [LabelCluster]     = _clusterName,
                [LabelKinds]       = string.Join(",", protoKinds.Distinct()),
                [LabelPort]        = _port.ToString(),
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
                    ClusterName           = clusterName,
                    Address               = address,
                    Port                  = port,
                    Kinds                 = kinds,
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

        public async Task Shutdown(Cluster cluster)
        {
            await DeregisterMemberAsync(cluster);
            cluster.System.Root.Stop(_clusterMonitor);
        }

        public void MonitorMemberStatusChanges(Cluster cluster) => cluster.System.Root.Send(_clusterMonitor, new StartWatchingCluster(_clusterName));

        PID                          _clusterMonitor;
        string                       _clusterName;
        string                       _address;
        int                          _port;
        string[]                     _kinds;
        string                       _podName;

        
        
        
        public Task StartMemberAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            MemberList memberList)
        {
            throw new NotImplementedException();
        }

        public Task StartClientAsync(Cluster cluster, string clusterName, string host, int port, MemberList memberList)
        {
            throw new NotImplementedException();
        }

        public Task ShutdownAsync(bool graceful)
        {
            throw new NotImplementedException();
        }

        public Task UpdateClusterState(ClusterState state)
        {
            throw new NotImplementedException();
        }
    }
}