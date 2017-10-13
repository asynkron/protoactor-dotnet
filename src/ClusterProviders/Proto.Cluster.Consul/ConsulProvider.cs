// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
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
        private string _id;
        private string _clusterName;
        private string _address;
        private int _port;
        private int _weight;
        private string[] _kinds;
        private TimeSpan _serviceTtl;
        private TimeSpan _blockingWaitTime;
        private TimeSpan _deregisterCritical;
        private TimeSpan _refreshTtl;
        private ulong _index;
        private bool _shutdown = false;
        private bool _deregistered = false;

        public ConsulProvider(ConsulProviderOptions options) : this(options, config => { }) { }

        public ConsulProvider(ConsulProviderOptions options, Action<ConsulClientConfiguration> consulConfig)
        {
            _serviceTtl = options.ServiceTtl.Value;
            _refreshTtl = options.RefreshTtl.Value;
            _deregisterCritical = options.DeregisterCritical.Value;
            _blockingWaitTime = options.BlockingWaitTime.Value;

            _client = new ConsulClient(consulConfig);
        }

        public ConsulProvider(IOptions<ConsulProviderOptions> options) : this(options.Value, config => { }) { }

        public ConsulProvider(IOptions<ConsulProviderOptions> options, Action<ConsulClientConfiguration> consulConfig) : this(options.Value, consulConfig) { }

        public async Task RegisterMemberAsync(string clusterName, string address, int port, int weight, string[] kinds)
        {
            _id = $"{clusterName}@{address}:{port}";
            _clusterName = clusterName;
            _address = address;
            _port = port;
            _weight = weight;
            _kinds = kinds;
            _index = 0;

            await RegisterServiceAsync();
            await RegisterProcessAsync();

            await BlockingUpdateTtlAsync();
            await BlockingStatusChangeAsync();
            UpdateTtl();
        }

        public async Task DeregisterMemberAsync()
        {
            //DeregisterService
            await DeregisterServiceAsync();
            //DeleteProcess
            await DeregisterProcessAsync();

            _deregistered = true;
        }

        public async Task DeregisterAllKindsAsync()
        {
            this._kinds = new string[0];
            await RegisterServiceAsync();
        }

        public async Task UpdateWeight(int weight)
        {
            this._weight = weight;
            if (!string.IsNullOrEmpty(this._address))
                await RegisterProcessAsync();
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

        private async Task RegisterServiceAsync()
        {
            var s = new AgentServiceRegistration
            {
                ID = _id,
                Name = _clusterName,
                Tags = _kinds.ToArray(),
                Address = _address,
                Port = _port,
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

        private async Task RegisterProcessAsync()
        {
            //register a semi unique ID for the current process
            var kvKey = $"{_clusterName}/{_address}:{_port}"; //slash should be present
            var value = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssK") + Environment.NewLine + _weight);
            await _client.KV.Put(new KVPair(kvKey)
            {
                //Write the ID for this member.
                //the value is later used to see if an existing node have changed its ID over time
                //meaning that it has Re-joined the cluster.
                Value = value
            }, new WriteOptions());
        }

        private async Task DeregisterProcessAsync()
        {
            var kvKey = $"{_clusterName}/{_address}:{_port}"; //slash should be present
            await _client.KV.Delete(kvKey);
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

            var memberIds = new Dictionary<string, string>();
            foreach (var v in kv.Response)
            {
                //Read the ID per member.
                //The value is used to see if an existing node have changed its ID over time
                //meaning that it has Re-joined the cluster.
                memberIds[v.Key] = Encoding.UTF8.GetString(v.Value);
            }

            (string memberId, int weight) DecompileConsulValue(string mIdKey)
            {
                if (memberIds.TryGetValue(mIdKey, out string v))
                {
                    var ps = v.Split('\n');
                    return (ps[0], ps.Length > 0 ? int.Parse(ps[1]) : 1);
                }
                else return (null, 5);
            }

            var memberStatuses =
                from v in statuses.Response
                let memberIdKey = $"{_clusterName}/{v.Service.Address}:{v.Service.Port}"
                let val = DecompileConsulValue(memberIdKey)
                where val.memberId != null
                let passing = Equals(v.Checks[1].Status, HealthStatus.Passing)
                select new MemberStatus(val.memberId, v.Service.Address, v.Service.Port, v.Service.Tags, passing, val.weight);

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

        private async Task BlockingUpdateTtlAsync()
        {
            await _client.Agent.PassTTL("service:" + _id, "");
        }
    }
}