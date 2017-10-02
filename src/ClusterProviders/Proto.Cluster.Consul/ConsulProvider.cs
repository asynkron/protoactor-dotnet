// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.Consul
{
    public class ConsulProviderOptions
    {
        /// <summary>
        /// Default value is 3 seconds
        /// </summary>
        public TimeSpan? ServiceTtl { get; set; } = TimeSpan.FromSeconds(3);

        /// <summary>
        /// Default value is 1 second
        /// </summary>
        public TimeSpan? RefreshTtl { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Default value is 10 seconds
        /// </summary>
        public TimeSpan? DeregisterCritical { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default value is 20 seconds
        /// </summary>
        public TimeSpan? BlockingWaitTime { get; set; } = TimeSpan.FromSeconds(20);
    }

    public class ConsulProvider : IClusterProvider
    {
        private readonly ConsulClient _client;
        private string _clusterName;
        private readonly TimeSpan _serviceTtl;
        private readonly TimeSpan _blockingWaitTime;
        private readonly TimeSpan _deregisterCritical;
        private readonly TimeSpan _refreshTtl;
        private string _id;
        private ulong _index;
        private string _kvKey;
        private bool _shutdown = false;
        private bool _deregistered = false;

        public ConsulProvider(ConsulProviderOptions options) : this(options, config => { })
        {
        }

        public ConsulProvider(ConsulProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _serviceTtl = options.ServiceTtl.Value;
            _refreshTtl = options.RefreshTtl.Value;
            _deregisterCritical = options.DeregisterCritical.Value;
            _blockingWaitTime = options.BlockingWaitTime.Value;

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


            //register a semi unique ID for the current process
            _kvKey = $"{_clusterName}/{host}:{port}"; //slash should be present
            var value = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssK"));
            await _client.KV.Put(new KVPair(_kvKey)
                                 {
                                     //Write the ID for this member.
                                     //the value is later used to see if an existing node have changed its ID over time
                                     //meaning that it has Re-joined the cluster.
                                     Value = value
                                 }, new WriteOptions());

            await BlockingUpdateTtlAsync();
            await BlockingStatusChangeAsync();
            UpdateTtl();
        }

        public async Task DeregisterMemberAsync()
        {
            //Deregister
            await _client.Agent.ServiceDeregister(_id);
            //DeleteKeyValue
            await _client.KV.Delete(_kvKey);

            _deregistered = true;
        }

        public async Task Shutdown()
        {
            _shutdown = true;
            if (!_deregistered)
                await DeregisterMemberAsync();
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

            var memberIds = new Dictionary<string, long>();
            foreach (var v in kv.Response)
            {
                //Read the ID per member.
                //The value is used to see if an existing node have changed its ID over time
                //meaning that it has Re-joined the cluster.
                memberIds[v.Key] = BitConverter.ToInt64(v.Value, 0);
            }

            long? GetMemberId(string mIdKey)
            {
                if (memberIds.TryGetValue(mIdKey, out long v)) return v;
                else return null;
            };

            var memberStatuses =
                from v in statuses.Response
                let memberIdKey = $"{_clusterName}/{v.Service.Address}:{v.Service.Port}"
                let memberId = GetMemberId(memberIdKey)
                where memberId != null
                let passing = Equals(v.Checks[1].Status, HealthStatus.Passing)
                select new MemberStatus(memberId.Value, v.Service.Address, v.Service.Port, v.Service.Tags, passing);

            var res = new ClusterTopologyEvent(memberStatuses);
            Actor.EventStream.Publish(res);
        }

        private async Task BlockingUpdateTtlAsync()
        {
            await _client.Agent.PassTTL("service:" + _id, "");
        }
    }
}