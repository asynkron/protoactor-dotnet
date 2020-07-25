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
    public class TestProvider : IClusterProvider
    {
        private readonly TestProviderOptions _options;
        private readonly Action<ConsulClientConfiguration> _consulConfig;
        private PID _clusterMonitor;

        public TestProvider(TestProviderOptions options) : this(options, config => { }) { }

        public TestProvider(TestProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _options = options;
            _consulConfig = consulConfig;
        }

        public TestProvider(IOptions<TestProviderOptions> options) : this(options.Value, config => { }) { }

        public TestProvider(IOptions<TestProviderOptions> options, Action<ConsulClientConfiguration> consulConfig)
            : this(options.Value, consulConfig) { }

        public Task RegisterMemberAsync(Cluster cluster,
            string clusterName, string address, int port, string[] kinds, IMemberStatusValue statusValue,
            IMemberStatusValueSerializer statusValueSerializer
        )
        {
            var props = Props
                .FromProducer(() => new TestClusterMonitor(cluster.System, _options, _consulConfig))
                .WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy)
                .WithDispatcher(Mailbox.Dispatchers.SynchronousDispatcher);
            _clusterMonitor = cluster.System.Root.SpawnNamed(props, "ClusterMonitor");

            cluster.System.Root.Send(
                _clusterMonitor, new Messages.RegisterMember
                {
                    ClusterName = clusterName,
                    Address = address,
                    Port = port,
                    Kinds = kinds,
                    StatusValue = statusValue,
                    StatusValueSerializer = statusValueSerializer
                }
            );

            return Actor.Done;
        }

        public Task DeregisterMemberAsync(Cluster cluster)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.DeregisterMember());
            return Actor.Done;
        }

        public Task Shutdown(Cluster cluster)
        {
            cluster.System.Root.Stop(_clusterMonitor);
            return Task.CompletedTask;
        }

        public void MonitorMemberStatusChanges(Cluster cluster) => cluster.System.Root.Send(_clusterMonitor, new Messages.CheckStatus { Index = 0 });

        public Task UpdateMemberStatusValueAsync(Cluster cluster, IMemberStatusValue statusValue)
        {
            cluster.System.Root.Send(_clusterMonitor, new Messages.UpdateStatusValue { StatusValue = statusValue });
            return Actor.Done;
        }
    }
}