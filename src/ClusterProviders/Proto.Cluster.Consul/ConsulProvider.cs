// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        public TimeSpan? ServiceTtl { get; set; } = TimeSpan.FromSeconds(10);

        /// <summary>
        /// Default value is 1 second
        /// </summary>
        public TimeSpan? RefreshTtl { get; set; } = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Default value is 10 seconds
        /// </summary>
        public TimeSpan? DeregisterCritical { get; set; } = TimeSpan.FromSeconds(30);

        /// <summary>
        /// Default value is 20 seconds
        /// </summary>
        public TimeSpan? BlockingWaitTime { get; set; } = TimeSpan.FromSeconds(20);
    }

    public class ConsulProvider : IClusterProvider
    {
        private readonly ConsulClient _client;
        private string _id;
        private string _clusterName;
        private string _address;
        private int _port;
        private string[] _kinds;
        private TimeSpan _serviceTtl;
        private TimeSpan _blockingWaitTime;
        private TimeSpan _deregisterCritical;
        private TimeSpan _refreshTtl;
        private ulong _index;
        private bool _shutdown = false;
        private bool _deregistered = false;
        private IMemberStatusValue _statusValue;
        private IMemberStatusValueSerializer _statusValueSerializer;

        public ConsulProvider(ConsulProviderOptions options) : this(options, config => { }) { }

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

        public async Task RegisterMemberAsync(string clusterName, string address, int port, string[] kinds, IMemberStatusValue statusValue, IMemberStatusValueSerializer statusValueSerializer)
        {
            _id = $"{clusterName}@{address}:{port}";
            _clusterName = clusterName;
            _address = address;
            _port = port;
            _kinds = kinds;
            _index = 0;
            _statusValue = statusValue;
            _statusValueSerializer = statusValueSerializer;

            await RegisterServiceAsync();

            UpdateTtl();
        }

        public async Task DeregisterMemberAsync()
        {
            //DeregisterService
            await DeregisterServiceAsync();

            _deregistered = true;
        }

        public async Task DeregisterAllKindsAsync()
        {
            this._kinds = new string[0];
            await RegisterServiceAsync();
        }

        public async Task Shutdown()
        {
            _shutdown = true;
            if (!_deregistered)
                await DeregisterMemberAsync();
        }

        public void MonitorMemberStatusChanges()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    NotifyStatuses();
                }
            }) {IsBackground = true};
            t.Start();
        }

        private void UpdateTtl()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    BlockingUpdateTtl();
                    Thread.Sleep(_refreshTtl);
                }
            }) {IsBackground = true};
            t.Start();
        }

        private async Task RegisterServiceAsync()
        {
            var s = new AgentServiceRegistration
            {
                ID = _id,
                Name = _clusterName,
                Tags = _kinds.ToArray(),
                Address = _address,
                Port = _port,
                Meta = new Dictionary<string, string>() { { "StatusValue", _statusValueSerializer.Serialize(_statusValue) } },
                Check = new AgentServiceCheck
                {
                    DeregisterCriticalServiceAfter = _deregisterCritical,
                    TTL = _serviceTtl
                }
            };
            await _client.Agent.ServiceRegister(s);
        }

        private async Task DeregisterServiceAsync()
        {
            await _client.Agent.ServiceDeregister(_id);
        }

        public async Task UpdateMemberStatusValueAsync(IMemberStatusValue statusValue)
        {
            _statusValue = statusValue;
            await this.RegisterServiceAsync();
        }

        private void NotifyStatuses()
        {
            var statuses = _client.Health.Service(_clusterName, null, false, new QueryOptions
            {
                WaitIndex = _index,
                WaitTime = _blockingWaitTime
            }).Result;
            _index = statuses.LastIndex;
            var memberStatuses =
                (from v in statuses.Response
                 let memberId = v.Service.ID
                 let memberStatusVal = v.Service.Meta["StatusValue"]
                 select new MemberStatus(memberId, v.Service.Address, v.Service.Port, v.Service.Tags, true, _statusValueSerializer.Deserialize(memberStatusVal)))
                .ToArray();

            //Update Tags for this member
            foreach (var memStat in memberStatuses)
            {
                if (memStat.Address == _address && memStat.Port == _port)
                {
                    _kinds = memStat.Kinds.ToArray();
                    break;
                }
            }

            var res = new ClusterTopologyEvent(memberStatuses);
            Actor.EventStream.Publish(res);
        }

        private void BlockingUpdateTtl()
        {
            _client.Agent.PassTTL("service:" + _id, "").Wait();
        }
    }
}