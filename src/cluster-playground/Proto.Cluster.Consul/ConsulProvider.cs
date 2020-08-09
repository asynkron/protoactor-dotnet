// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.Consul
{
    public class ConsulProvider : IClusterProvider
    {
        private readonly ConsulProviderOptions _options;
        private readonly Action<ConsulClientConfiguration> _consulConfig;
        private PID _clusterMonitor;

        public ConsulProvider(ConsulProviderOptions options) : this(options, config => { }) { }

        public ConsulProvider(ConsulProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _options = options;
            _consulConfig = consulConfig;
        }

        public ConsulProvider(IOptions<ConsulProviderOptions> options) : this(options.Value, config => { }) { }

        public ConsulProvider(IOptions<ConsulProviderOptions> options, Action<ConsulClientConfiguration> consulConfig)
            : this(options.Value, consulConfig) { }



        public Task DeregisterMemberAsync(Cluster cluster)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.DeregisterMember());
            return Actor.Done;
        }
        

        public void MonitorMemberStatusChanges(Cluster cluster) => cluster.System.Root.Send(_clusterMonitor, new Messages.CheckStatus { Index = 0 });

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.UpdateStatusValue { StatusValue = statusValue });
            return Actor.Done;
        }

        public Task StartAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            IMemberStatusValue? statusValue, IMemberStatusValueSerializer serializer, MemberList memberList)
        {
            var props = Props
                .FromProducer(() => new ConsulClusterMonitor(cluster.System, _options, _consulConfig))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);
            _clusterMonitor = cluster.System.Root.SpawnNamed(props, "ClusterMonitor");

            cluster.System.Root.Send(
                _clusterMonitor, new Messages.RegisterMember
                {
                    ClusterName = clusterName,
                    Address = host,
                    Port = port,
                    Kinds = kinds,
                    StatusValue = statusValue,
                    StatusValueSerializer = serializer
                }
            );

            return Actor.Done;
        }

        public Task ShutdownAsync(Cluster cluster)
        {
            cluster.System.Root.Stop(_clusterMonitor);
            return Task.CompletedTask;
        }
    }
}