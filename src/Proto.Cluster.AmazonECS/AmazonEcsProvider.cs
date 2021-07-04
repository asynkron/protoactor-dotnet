// -----------------------------------------------------------------------
// <copyright file="AmazonEcsProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.ECS;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Proto.Utils;
using static Proto.Cluster.AmazonECS.Messages;

namespace Proto.Cluster.AmazonECS
{
    [PublicAPI]
    public class AmazonEcsProvider : IClusterProvider
    {
        private static readonly ILogger Logger = Log.CreateLogger<AmazonEcsProvider>();

        private string _address;
        private Cluster _cluster;
        
        private string _clusterName;
        private string _host;
        private string[] _kinds;
        private MemberList _memberList;
        private string _taskArn;
        private int _port;
        private readonly AmazonEcsProviderConfig _config;
        private readonly AmazonECSClient _client;
        private readonly string _ecsClusterName;

        public AmazonEcsProvider(AmazonECSClient client,string ecsClusterName, string taskArn , AmazonEcsProviderConfig config)
        {
            _ecsClusterName = ecsClusterName;
            _client = client;
            _config = config;
            _taskArn = taskArn;
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
            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(bool graceful) => await DeregisterMemberAsync(_cluster);

        public async Task RegisterMemberAsync()
        {
            await Retry.Try(RegisterMemberInner, onError: OnError, onFailed: OnFailed);

            static void OnError(int attempt, Exception exception) => Logger.LogWarning(exception, "Failed to register service");

            static void OnFailed(Exception exception) => Logger.LogError(exception, "Failed to register service");
        }

        public async Task RegisterMemberInner()
        {
            Logger.LogInformation("[Cluster][AmazonEcsProvider] Registering service {PodName} on {PodIp}", _taskArn, _address);

            var tags = new Dictionary<string, string>
            {
                [ProtoLabels.LabelCluster] = _clusterName,
                [ProtoLabels.LabelPort] = _port.ToString(),
                [ProtoLabels.LabelMemberId] = _cluster.System.Id
            };

            foreach (var kind in _kinds)
            {
                var labelKey = $"{ProtoLabels.LabelKind}-{kind}";
                tags.TryAdd(labelKey, "true");
            }

            try
            {
                await _client.UpdateMetadata(_taskArn, tags);
            }
            catch(Exception x)
            {
                Logger.LogError(x, "Failed to update metadata");
            }
        }

        private void StartClusterMonitor()
        {
            _ = SafeTask.Run(async () => {

                    while (!_cluster.System.Shutdown.IsCancellationRequested)
                    {
                        Logger.Log(_config.DebugLogLevel, "Calling ECS API");

                        try
                        {
                            var members = await _client.GetMembers(_ecsClusterName);
                            

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

                        await Task.Delay(_config.PollIntervalSeconds);
                    }
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
            Logger.LogInformation("[Cluster][AmazonEcsProvider] Unregistering service {PodName} on {PodIp}", _taskArn, _address);
            await _client.UpdateMetadata(_taskArn, new Dictionary<string, string>());
        }
    }
}