// -----------------------------------------------------------------------
// <copyright file="AmazonEcsProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Mailbox;
using Proto.Utils;
using static Proto.Cluster.AmazonECS.Messages;
using static Proto.Cluster.AmazonECS.ProtoLabels;

namespace Proto.Cluster.AmazonECS
{
    [PublicAPI]
    public class AmazonEcsProvider : IClusterProvider
    {
        private static readonly ILogger Logger = Log.CreateLogger<AmazonEcsProvider>();

        private string _address;
        private Cluster _cluster;

        private PID _clusterMonitor;
        private string _clusterName;
        private string _host;
        private string[] _kinds;
        private MemberList _memberList;
        private string _podName;
        private int _port;
        private readonly AmazonEcsProviderConfig _config;

        public AmazonEcsProvider() : this(new AmazonEcsProviderConfig())
        {
        }

        public AmazonEcsProvider(AmazonEcsProviderConfig config)
        {
            _config = config;
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
            await Retry.Try(RegisterMemberInner, onError: OnError, onFailed: OnFailed);

            static void OnError(int attempt, Exception exception) => Logger.LogWarning(exception, "Failed to register service");

            static void OnFailed(Exception exception) => Logger.LogError(exception, "Failed to register service");
        }

        public async Task RegisterMemberInner()
        {
            Logger.LogInformation("[Cluster][AmazonEcsProvider] Registering service {PodName} on {PodIp}", _podName, _address);

            //var pod = await _AmazonEcs.ReadNamespacedPodAsync(_podName, AmazonEcsExtensions.GetKubeNamespace());
          //  if (pod is null) throw new ApplicationException($"Unable to get own pod information for {_podName}");

         //   Logger.LogInformation("[Cluster][AmazonEcsProvider] Using AmazonEcs namespace: " + pod.Namespace());

          

         
            Logger.LogInformation("[Cluster][AmazonEcsProvider] Using AmazonEcs port: " + _port);

          //  var existingLabels = pod.Metadata.Labels;

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
            // foreach (var existing in existingLabels)
            // {
            //     labels.TryAdd(existing.Key, existing.Value);
            // }

            // try
            // {
            //     await _AmazonEcs.ReplacePodLabels(_podName, AmazonEcsExtensions.GetKubeNamespace(), labels);
            // }
            // catch (HttpOperationException e)
            // {
            //     Logger.LogError(e, "[Cluster][AmazonEcsProvider] Unable to update pod labels, registration failed");
            //     throw;
            // }
        }

        private void StartClusterMonitor()
        {
            var props = Props
                .FromProducer(() => new AmazonEcsClusterMonitor(_cluster, _config))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Dispatchers.SynchronousDispatcher);
            _clusterMonitor = _cluster.System.Root.SpawnNamed(props, "ClusterMonitor");
        //    _podName = AmazonEcsExtensions.GetPodName();

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
            await Retry.Try(() => DeregisterMemberInner(cluster), onError: OnError, onFailed: OnFailed);

            static void OnError(int attempt, Exception exception) => Logger.LogWarning(exception, "Failed to deregister service");

            static void OnFailed(Exception exception) => Logger.LogError(exception, "Failed to deregister service");
        }

        private async Task DeregisterMemberInner(Cluster cluster)
        {
            Logger.LogInformation("[Cluster][AmazonEcsProvider] Unregistering service {PodName} on {PodIp}", _podName, _address);

           // var kubeNamespace = AmazonEcsExtensions.GetKubeNamespace();

           // var pod = await _AmazonEcs.ReadNamespacedPodAsync(_podName, kubeNamespace);

            foreach (var kind in _kinds)
            {
                try
                {
                    var labelKey = $"{LabelKind}-{kind}";
           //         pod.SetLabel(labelKey, null);
                }
                catch (Exception x)
                {
                    Logger.LogError(x, "[Cluster][AmazonEcsProvider] Failed to remove label");
                }
            }

            //pod.SetLabel(LabelCluster, null);

        //    await _AmazonEcs.ReplacePodLabels(_podName, kubeNamespace, pod.Labels());

            cluster.System.Root.Send(_clusterMonitor, new DeregisterMember());
        }

        public void MonitorMemberStatusChanges() => _cluster.System.Root.Send(_clusterMonitor, new StartWatchingCluster(_clusterName));
    }
}