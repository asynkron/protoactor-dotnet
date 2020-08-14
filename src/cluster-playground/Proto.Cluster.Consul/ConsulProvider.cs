// -----------------------------------------------------------------------
//   <copyright file="ConsulProvider.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Consul;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Proto.Cluster.Consul
{
    //TLDR;
    //this class has a very simple responsibility, poll consul for status updates.
    //then transform these statuses to MemberStatus messages and pass on to the 
    //cluster MemberList instance
    //
    //Helper functionality: register, deregister and refresh TTL
    public class ConsulProvider : IClusterProvider
    {
        private ILogger _logger;
        private readonly TimeSpan _blockingWaitTime;
        private readonly ConsulClient _client;
        private readonly TimeSpan _deregisterCritical; //this is how long the service exists in consul before disappearing when unhealthy, min 1 min
        private readonly TimeSpan _serviceTtl; //this is how long the service is healthy without a ttl refresh
        private readonly TimeSpan _refreshTtl;  //this is the refresh rate of TTL, should be smaller than the above
        private string _address;

        private Cluster _cluster;
        private string _consulServiceName; //name of the custer, in consul this means the name of the service
        private volatile bool _deregistered;
        private string _consulServiceInstanceId; //the specific instance id of this node in consul
 
        private ulong _index;
        private string[] _kinds;
        private MemberList _memberList;
        private int _port;
        private bool _shutdown;

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

        public ConsulProvider(IOptions<ConsulProviderOptions> options, Action<ConsulClientConfiguration> consulConfig) :
            this(options.Value, consulConfig)
        {
        }

        public async Task StartAsync(Cluster cluster, string clusterName, string host, int port, string[] kinds,
            MemberList memberList)
        {
            _cluster = cluster;
            _consulServiceInstanceId = $"{clusterName}-{_cluster.Id}@{host}:{port}";
            _consulServiceName = clusterName;
            _address = host;
            _port = port;
            _kinds = kinds;
            _index = 0;
            _memberList = memberList;
            
            _logger = Log.CreateLogger($"ConsulProvider-{_cluster.Id}");

            await RegisterMemberAsync();

            StartUpdateTtlLoop();
            StartMonitorMemberStatusChangesLoop();
        }


        public async Task ShutdownAsync(bool graceful)
        {
            
            _logger.LogInformation("Shutting down consul provider");
            //flag for shutdown. used in thread loops
            _shutdown = true;
            if (!graceful)
            {
                return;
            }

            //DeregisterService
            await DeregisterServiceAsync();

            _deregistered = true;
        }


        //do not move to thread pool, a contended system will interfere with cluster stability 
        private void StartMonitorMemberStatusChangesLoop()
        {
            var t = new Thread(_ =>
                {
                    while (!_shutdown)
                    {
                        BlockingNotifyStatuses();
                    }
                }
            ) {IsBackground = true};
            t.Start();
        }


        //do not move to thread pool, a contended system will interfere with cluster stability
        private void StartUpdateTtlLoop()
        {
            var t = new Thread(_ =>
                {
                    while (!_shutdown)
                    {
                        _client.Agent.PassTTL("service:" + _consulServiceInstanceId, "").Wait();
                        Thread.Sleep(_refreshTtl);
                    }

                    _logger.LogInformation("Exiting TTL loop");
                }
            ) {IsBackground = true};
            t.Start();
        }

        //register this cluster in consul.
        private async Task RegisterMemberAsync()
        {
            var s = new AgentServiceRegistration
            {
                ID = _consulServiceInstanceId,
                Name = _consulServiceName,
                Tags = _kinds.ToArray(),
                Address = _address,
                Port = _port,
                Check = new AgentServiceCheck
                {
                    DeregisterCriticalServiceAfter = _deregisterCritical,
                    TTL = _serviceTtl,
                    
                },
                Meta = new Dictionary<string,string>
                {
                    //register a unique ID for the current process
                    //if a node with host X and port Y, joins, then leaves, then joins again.
                    //we need a way to distinguish the new node from the old node.
                    //this is what this ID is for
                    {"id",_cluster.Id.ToString()}
                }
            };
            await _client.Agent.ServiceRegister(s);
        }


        //unregister this cluster from consul
        private async Task DeregisterServiceAsync()
        {
            await _client.Agent.ServiceDeregister(_consulServiceInstanceId);
            _logger.LogInformation("Deregistered service");
        }

        private void BlockingNotifyStatuses()
        {
            var statuses = _client.Health.Service(_consulServiceName, null, false, new QueryOptions
                {
                    WaitIndex = _index,
                    WaitTime = _blockingWaitTime
                }
            ).Result;
            if (_deregistered)
            {
                return;
            }
            _logger.LogDebug("Got status updates from Consul");

            _index = statuses.LastIndex;
            
            var memberStatuses =
                statuses
                    .Response
                    .Where(v => IsAlive(v.Checks)) //only include members that are alive
                    .Select(v => new MemberStatus(
                            Guid.Parse(v.Service.Meta["id"]), 
                            v.Service.Address, 
                            v.Service.Port, 
                            v.Service.Tags)
                    )
                .ToArray();

            //why is this not updated via the ClusterTopologyEvents?
            //because following events is messy
            _memberList.UpdateClusterTopology(memberStatuses);
            var res = new ClusterTopologyEvent(memberStatuses);
            _cluster.System.EventStream.Publish(res);
        }

        private static bool IsAlive(HealthCheck[] serviceChecks) => 
            serviceChecks.All(c => c.Status == HealthStatus.Passing);
    }
}