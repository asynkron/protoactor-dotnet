// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            await RegisterMemberValsAsync();

            UpdateTtl();
        }

        public async Task DeregisterMemberAsync()
        {
            //DeregisterService
            await DeregisterServiceAsync();
            //DeleteProcess
            await DeregisterMemberValsAsync();

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

            if (_statusValue == null) return;
            
            if (string.IsNullOrEmpty(_id)) return;

            //register a semi unique ID for the current process
            var kvKey = $"{_clusterName}/{_address}:{_port}/StatusValue"; //slash should be present
            var value = _statusValueSerializer.ToValueBytes(statusValue);
            await _client.KV.Put(new KVPair(kvKey)
            {
                //Write the ID for this member.
                //the value is later used to see if an existing node have changed its ID over time
                //meaning that it has Re-joined the cluster.
                Value = value
            }, new WriteOptions());
        }

        private async Task RegisterMemberValsAsync()
        {
            var txn = new List<KVTxnOp>();

            //register a semi unique ID for the current process
            var kvKey = $"{_clusterName}/{_address}:{_port}/ID"; //slash should be present
            var value = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssK"));
            txn.Add(new KVTxnOp(kvKey, KVTxnVerb.Set) { Value = value });

            if (_statusValue != null)
            {
                var statusValKey = $"{_clusterName}/{_address}:{_port}/StatusValue"; //slash should be present
                var statusValValue = _statusValueSerializer.ToValueBytes(_statusValue);
                txn.Add(new KVTxnOp(statusValKey, KVTxnVerb.Set) { Value = statusValValue });
            }

            await _client.KV.Txn(txn, new WriteOptions());
        }

        private async Task DeregisterMemberValsAsync()
        {
            var kvKey = $"{_clusterName}/{_address}:{_port}"; //slash should be present
            await _client.KV.DeleteTree(kvKey);
        }

        private void NotifyStatuses()
        {
            var statuses = _client.Health.Service(_clusterName, null, false, new QueryOptions
            {
                WaitIndex = _index,
                WaitTime = _blockingWaitTime
            }).Result;
            _index = statuses.LastIndex;
            var kvKey = _clusterName + "/";
            var kv = _client.KV.List(kvKey).Result;

            var memberIds = new Dictionary<string, string>();
            var memberStatusVals = new Dictionary<string, byte[]>();
            foreach (var v in kv.Response)
            {
                var idx = v.Key.LastIndexOf('/');
                var key = v.Key.Substring(0, idx);
                var type = v.Key.Substring(idx + 1);
                if (type == "ID")
                {
                    //Read the ID per member.
                    //The value is used to see if an existing node have changed its ID over time
                    //meaning that it has Re-joined the cluster.
                    memberIds[key] = Encoding.UTF8.GetString(v.Value);
                }
                else if (type == "StatusValue")
                {
                    memberStatusVals[key] = v.Value;
                }
            }

            string GetMemberId(string mIdKey)
            {
                if (memberIds.TryGetValue(mIdKey, out string v)) return v;
                else return null;
            };

            byte[] GetMemberStatusVal(string mIdKey)
            {
                if (memberStatusVals.TryGetValue(mIdKey, out byte[] v)) return v;
                else return null;
            };

            var memberStatuses =
                (from v in statuses.Response
                    let memberIdKey = $"{_clusterName}/{v.Service.Address}:{v.Service.Port}"
                    let memberId = GetMemberId(memberIdKey)
                    where memberId != null
                    let memberStatusVal = GetMemberStatusVal(memberIdKey)
                    select new MemberStatus(memberId, v.Service.Address, v.Service.Port, v.Service.Tags, true, _statusValueSerializer.FromValueBytes(memberStatusVal)))
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