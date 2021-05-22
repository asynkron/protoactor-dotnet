// -----------------------------------------------------------------------
// <copyright file="KubernetesClusterMonitor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Proto.Cluster.Events;
using static Proto.Cluster.Kubernetes.Messages;
using static Proto.Cluster.Kubernetes.ProtoLabels;

namespace Proto.Cluster.Kubernetes
{
    class KubernetesClusterMonitor : IActor
    {
        private static readonly ILogger Logger = Log.CreateLogger<KubernetesClusterMonitor>();
        private readonly Cluster _cluster;

        private readonly Dictionary<string, V1Pod> _clusterPods = new();
        private readonly IKubernetes _kubernetes;

        private string _address;

        private string _clusterName;
        private string _podName;
        private bool _stopping;
        private Watcher<V1Pod> _watcher;
        private Task<HttpOperationResponse<V1PodList>> _watcherTask;
        private bool _watching;

        public KubernetesClusterMonitor(Cluster cluster, IKubernetes kubernetes)
        {
            _cluster = cluster;
            _kubernetes = kubernetes;
        }

        public Task ReceiveAsync(IContext context) => context.Message switch
        {
            RegisterMember cmd       => Register(cmd),
            StartWatchingCluster cmd => StartWatchingCluster(cmd.ClusterName, context),
            DeregisterMember         => StopWatchingCluster(),
            Stopping                 => StopWatchingCluster(),
            _                        => Task.CompletedTask
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
                    "[Cluster] Stopping monitoring for {PodName} with ip {PodIp}",
                    _podName, _address
                );

                _stopping = true;
                _watcher.Dispose();
                _watcherTask.Dispose();
            }

            return Task.CompletedTask;
        }

        private Task StartWatchingCluster(string clusterName, ISenderContext context)
        {
            var selector = $"{LabelCluster}={clusterName}";
            Logger.LogDebug("[Cluster] Starting to watch pods with {Selector}", selector);

            _watcherTask = _kubernetes.ListNamespacedPodWithHttpMessagesAsync(
                KubernetesExtensions.GetKubeNamespace(),
                labelSelector: selector,
                watch: true
            );
            _watcher = _watcherTask.Watch<V1Pod, V1PodList>(Watch, Error, Closed);
            _watching = true;

            void Error(Exception ex)
            {
                // If we are already in stopping state, just ignore it
                if (_stopping) return;

                // We log it and attempt to watch again, overcome transient issues
                Logger.LogInformation("[Cluster] Unable to watch the cluster status: {Error}", ex.Message);
                Restart();
            }

            // The watcher closes from time to time and needs to be restarted
            void Closed()
            {
                // If we are already in stopping state, just ignore it
                if (_stopping) return;

                Logger.LogDebug("[Cluster] Watcher has closed, restarting");
                Restart();
            }

            void Restart()
            {
                _watching = false;
                _watcher?.Dispose();
                _watcherTask?.Dispose();
                context.Send(context.Self!, new StartWatchingCluster(_clusterName));
            }

            return Task.CompletedTask;
        }

        private void Watch(WatchEventType eventType, V1Pod eventPod)
        {
            var podLabels = eventPod.Metadata.Labels;

            if (!podLabels.TryGetValue(LabelCluster, out var podClusterName))
            {
                Logger.LogInformation(
                    "[Cluster] The pod {PodName} is not a Proto.Cluster node",
                    eventPod.Metadata.Name
                );
                return;
            }

            if (_clusterName != podClusterName)
            {
                Logger.LogInformation(
                    "[Cluster] The pod {PodName} is from another cluster {Cluster}",
                    eventPod.Metadata.Name, _clusterName
                );
                return;
            }

            // Update the list of known pods
            if (eventType == WatchEventType.Deleted)
                _clusterPods.Remove(eventPod.Uid());
            else
                _clusterPods[eventPod.Uid()] = eventPod;

            var memberStatuses = _clusterPods.Values
                .Select(x => x.GetMemberStatus())
                .Where(x => x.IsCandidate)
                .Select(x => x.Status)
                .ToList();

            _cluster.MemberList.UpdateClusterTopology(memberStatuses);
        }
    }
}