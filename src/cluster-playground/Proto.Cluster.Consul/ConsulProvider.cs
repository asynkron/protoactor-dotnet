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
    public class ConsulProvider : IClusterProvider
    {
        private readonly ConsulClient _client;
        private string _id;
        private string _clusterName;
        private string _address;
        private int _port;
        private string[] _kinds;
        private readonly TimeSpan _serviceTtl;
        private readonly TimeSpan _blockingWaitTime;
        private readonly TimeSpan _deregisterCritical;
        private readonly TimeSpan _refreshTtl;
        private ulong _index;
        private bool _shutdown;
        private bool _deregistered;

        private Cluster _cluster;
        private MemberList _memberlist;

        public ConsulProvider( ConsulProviderOptions options) : this(options, config => { }) { }

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

        public async Task StartAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            IMemberStatusValue? statusValue, IMemberStatusValueSerializer serializer, MemberList memberList)
        {
            _cluster = cluster;
            _id = $"{clusterName}@{host}:{port}";
            _clusterName = clusterName;
            _address = host;
            _port = port;
            _kinds = kinds;
            _index = 0;

            _memberlist = memberList;

            await RegisterServiceAsync();
            await RegisterMemberValsAsync();

            UpdateTtl();
            MonitorMemberStatusChanges();
        }


        public async Task ShutdownAsync(bool graceful)
        {
            Console.WriteLine("Shutting down consul provider");
            //flag for shutdown. used in thread loops
            _shutdown = true;
            if (!graceful)
            {
                return;
            }
            //DeregisterService
            await DeregisterServiceAsync();
            //DeleteProcess
            await DeregisterMemberValsAsync();

            _deregistered = true;
        }
        
        
        //do not move to thread pool, a contended system will interfere with cluster stability 
        private void MonitorMemberStatusChanges()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    BlockingNotifyStatuses();
                }
            }) {IsBackground = true};
            t.Start();
        }

        
        //do not move to thread pool, a contended system will interfere with cluster stability
        private void UpdateTtl()
        {
            var t = new Thread(_ =>
            {
                while (!_shutdown)
                {
                    _client.Agent.PassTTL("service:" + _id, "").Wait();
                    Thread.Sleep(_refreshTtl);
                }
                
                Console.WriteLine("Exiting TTL loop");
            }) {IsBackground = true};
            t.Start();
        }

        //register this cluster in consul.
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
        

        //unregister this cluster from consul
        private async Task DeregisterServiceAsync()
        {
            await _client.Agent.ServiceDeregister(_id);
        }
        
        private async Task RegisterMemberValsAsync()
        {
            var txn = new List<KVTxnOp>();

            //register a semi unique ID for the current process
            var kvKey = $"{_clusterName}/{_address}:{_port}/ID"; //slash should be present
            var value = Encoding.UTF8.GetBytes(DateTime.UtcNow.ToString("yyyy-MM-dd'T'HH:mm:ssK"));
            txn.Add(new KVTxnOp(kvKey, KVTxnVerb.Set) { Value = value });

            await _client.KV.Txn(txn, new WriteOptions());
        }

        private async Task DeregisterMemberValsAsync()
        {
            var kvKey = $"{_clusterName}/{_address}:{_port}"; //slash should be present
            await _client.KV.DeleteTree(kvKey);
        }

        private void BlockingNotifyStatuses()
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
                if (memberIds.TryGetValue(mIdKey, out var v)) return v;
                return null;
            };

         

            var memberStatuses =
                (from v in statuses.Response
                    let memberIdKey = $"{_clusterName}/{v.Service.Address}:{v.Service.Port}"
                    let memberId = GetMemberId(memberIdKey)
                    where memberId != null
                    select new MemberStatus(memberId, v.Service.Address, v.Service.Port, v.Service.Tags, true, null)
                        )
                .ToArray();

            //TODO: why was this ever here? I know what tags I have already?
            // //Update Tags for this member
            // foreach (var memStat in memberStatuses)
            // {
            //     if (memStat.Address == _address && memStat.Port == _port)
            //     {
            //         _kinds = memStat.Kinds.ToArray();
            //         break;
            //     }
            // }

            _memberlist.UpdateClusterTopology(memberStatuses);
            var res = new ClusterTopologyEvent(memberStatuses);
            _cluster.System.EventStream.Publish(res);
            
        }
    }
}