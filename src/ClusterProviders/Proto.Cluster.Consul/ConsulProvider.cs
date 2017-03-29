// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.Consul
{
    public class ConsulProviderOptions
    {
        public TimeSpan? ServiceTtl { get; set; }
        public TimeSpan? RefreshTtl { get; set; }
        public TimeSpan? DeregisterCritical { get; set; }
        public TimeSpan? BlockingWaitTime { get; set; }
    }

    public class ConsulProvider : IClusterProvider
    {
        private readonly ConsulClient _client;
        private string _clusterName;
        private TimeSpan _serviceTtl;
        private TimeSpan _blockingWaitTime;
        private TimeSpan _deregisterCritical;
        private TimeSpan _refreshTtl;
        private string _id;
        private ulong _index;
        private bool _shutdown = false;

        public ConsulProvider(ConsulProviderOptions options) : this(options, config => { })
        {
        }

        public ConsulProvider(ConsulProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _serviceTtl = options.ServiceTtl ?? TimeSpan.FromSeconds(3);
            _refreshTtl = options.RefreshTtl ?? TimeSpan.FromSeconds(1);
            _deregisterCritical = options.DeregisterCritical ?? TimeSpan.FromSeconds(10);
            _blockingWaitTime = options.BlockingWaitTime ?? TimeSpan.FromSeconds(20);

            _client = new ConsulClient(consulConfig);
        }

        public ConsulProvider(IOptions<ConsulProviderOptions> options) : this(options.Value, config => { })
        {
        }

        public ConsulProvider(IOptions<ConsulProviderOptions> options, Action<ConsulClientConfiguration> consulConfig) : this(options.Value, consulConfig)
        {
        }


        public async Task RegisterMemberAsync(string clusterName, string host, int port, string[] kinds)
        {
            _id = $"{clusterName}@{host}:{port}";
            _clusterName = clusterName;
            _index = 0;
            _serviceTtl = TimeSpan.FromSeconds(3);
            _refreshTtl = TimeSpan.FromSeconds(1);
            _deregisterCritical = TimeSpan.FromSeconds(10);
            _blockingWaitTime = TimeSpan.FromSeconds(20);


            var s = new AgentServiceRegistration
                    {
                        ID = _id,
                        Name = clusterName,
                        Tags = kinds,
                        Address = host,
                        Port = port,
                        Check = new AgentServiceCheck
                                {
                                    DeregisterCriticalServiceAfter = _deregisterCritical,
                                    TTL = _serviceTtl
                                }
                    };
            await _client.Agent.ServiceRegister(s);


            //register a unique ID for the current process
            //similar to UID for Akka ActorSystem
            //TODO: Orleans just use an int32 for the unique id called Generation.
            var kvKey = $"{_clusterName}/{host}:{port}"; //slash should be present
            await _client.KV.Put(new KVPair(kvKey)
                                 {
                                     Value = Encoding.UTF8.GetBytes(DateTime.Now.ToString(CultureInfo.InvariantCulture))
                                 }, new WriteOptions());

            await BlockingUpdateTtlAsync();
            await BlockingStatusChangeAsync();
            UpdateTtl();
        }

        public void MonitorMemberStatusChanges()
        {
            Task.Run(async () =>
            {
                while (!_shutdown)
                {
                    await NotifyStatusesAsync();
                }
            });
        }

        private void UpdateTtl()
        {
            Task.Run(async () =>
            {
                while (!_shutdown)
                {
                    await BlockingUpdateTtlAsync();
                    await Task.Delay(_refreshTtl);
                }
            });
        }

        private async Task BlockingStatusChangeAsync()
        {
            await NotifyStatusesAsync();
        }

        private async Task NotifyStatusesAsync()
        {
            var statuses = await _client.Health.Service(_clusterName, null, false, new QueryOptions
                                                                                   {
                                                                                       WaitIndex = _index,
                                                                                       WaitTime = _blockingWaitTime
                                                                                   });
            _index = statuses.LastIndex;
            var kvKey = _clusterName + "/";
            var kv = await _client.KV.List(kvKey);

            var kvMap = new Dictionary<string, string>();
            foreach (var v in kv.Response)
            {
                kvMap[v.Key] = Encoding.UTF8.GetString(v.Value);
            }
            var memberStatuses =
                from v in statuses.Response
                let key = $"{_clusterName}/{v.Service.Address}:{v.Service.Port}"
                let memberId = kvMap[key]
                let passing = Equals(v.Checks[1].Status, HealthStatus.Passing)
                select new MemberStatus(memberId, v.Service.Address, v.Service.Port, v.Service.Tags, passing);
            var res = new ClusterTopologyEvent(memberStatuses);
            Actor.EventStream.Publish(res);
        }

        private async Task BlockingUpdateTtlAsync()
        {
            await _client.Agent.PassTTL("service:" + _id, "");
        }
    }
}