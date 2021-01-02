// -----------------------------------------------------------------------
// <copyright file="ConsulProvider.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Consul;
using JetBrains.Annotations;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Proto.Cluster.Data;
using Proto.Cluster.Events;

namespace Proto.Cluster.Consul
{
    //TLDR;
    //this class has a very simple responsibility, poll consul for status updates.
    //then transform these statuses to MemberStatus messages and pass on to the 
    //cluster MemberList instance
    //
    //Helper functionality: register, deregister and refresh TTL

    [PublicAPI]
    public class ConsulProvider : IClusterProvider
    {
        private readonly TimeSpan _blockingWaitTime;
        private readonly ConsulClient _client;

        private readonly TimeSpan
            _deregisterCritical; //this is how long the service exists in consul before disappearing when unhealthy, min 1 min

        private readonly TimeSpan _refreshTtl; //this is the refresh rate of TTL, should be smaller than the above

        private readonly TimeSpan _serviceTtl; //this is how long the service is healthy without a ttl refresh

        private Cluster _cluster;
        private string _consulLeaderKey;
        private string _consulServiceInstanceId; //the specific instance id of this node in consul

        private string _consulServiceName; //name of the custer, in consul this means the name of the service

        //   private string _consulSessionId;
        private volatile bool _deregistered;
        private string _host;

        private string[] _kinds;
        private ILogger _logger;
        private MemberList _memberList;
        private int _port;
        private bool _shutdown;

        public ConsulProvider(ConsulProviderConfig config) : this(config, clientConfiguration => { })
        {
        }

        public ConsulProvider(ConsulProviderConfig config, Action<ConsulClientConfiguration> clientConfiguration)
        {
            _serviceTtl = config!.ServiceTtl;
            _refreshTtl = config!.RefreshTtl;
            _deregisterCritical = config!.DeregisterCritical;
            _blockingWaitTime = config!.BlockingWaitTime;
            _client = new ConsulClient(clientConfiguration);
        }

        public ConsulProvider(IOptions<ConsulProviderConfig> options) : this(options.Value, clientConfiguration => { })
        {
        }

        public ConsulProvider(
            IOptions<ConsulProviderConfig> options,
            Action<ConsulClientConfiguration> clientConfiguration
        ) :
            this(options.Value, clientConfiguration)
        {
        }

        public async Task StartMemberAsync(Cluster cluster)
        {
            var (host, port) = cluster.System.GetAddress();
            var kinds = cluster.GetClusterKinds();
            SetState(cluster, cluster.Config.ClusterName, host, port, kinds, cluster.MemberList);
            await RegisterMemberAsync();
            StartUpdateTtlLoop();
            StartMonitorMemberStatusChangesLoop();
            //   StartLeaderElectionLoop();
        }

        public Task StartClientAsync(Cluster cluster)
        {
            var (host, port) = cluster.System.GetAddress();
            SetState(cluster, cluster.Config.ClusterName, host, port, null, cluster.MemberList);

            StartMonitorMemberStatusChangesLoop();

            return Task.CompletedTask;
        }

        public async Task ShutdownAsync(bool graceful)
        {
            _logger.LogInformation("Shutting down consul provider");
            //flag for shutdown. used in thread loops
            _shutdown = true;

            if (graceful)
            {
                await DeregisterServiceAsync();
                _deregistered = true;
            }

            _logger.LogInformation("Shut down consul provider");
        }

        //TODO: this is never signalled to rest of cluster
        //it gets hidden until leader blocking wait ends
        public async Task UpdateClusterState(ClusterState state)
        {
            // var json = JsonConvert.SerializeObject(state.BannedMembers);
            // var kvp = new KVPair($"{_consulServiceName}/banned")
            // {
            //     Value = Encoding.UTF8.GetBytes(json)
            // };
            //
            // var updated = await _client.KV.Put(kvp);
            //
            // if (!updated.Response) _logger.LogError("Failed to update cluster state");
        }

        private void SetState(
            Cluster cluster,
            string clusterName,
            string host,
            int port,
            string[] kinds,
            MemberList memberList
        )
        {
            _cluster = cluster;
            _consulServiceInstanceId = $"{clusterName}-{_cluster.System.Id}@{host}:{port}";
            _consulServiceName = clusterName;
            _host = host;
            _port = port;
            _kinds = kinds;
            _memberList = memberList;
            _logger = Log.CreateLogger($"ConsulProvider-{_cluster.LoggerId}");
        }

        private void StartMonitorMemberStatusChangesLoop()
        {
            _ = Task.Run(async () => {
                    var waitIndex = 0ul;

                    while (!_shutdown && !_cluster.System.Shutdown.IsCancellationRequested)
                    {
                        var statuses = await _client.Health.Service(_consulServiceName, null, false, new QueryOptions
                            {
                                WaitIndex = waitIndex,
                                WaitTime = _blockingWaitTime
                            }
                            , _cluster.System.Shutdown
                        );
                        if (_deregistered) break;

                        _logger.LogDebug("Got status updates from Consul");

                        waitIndex = statuses.LastIndex;

                        var currentMembers =
                            statuses
                                .Response
                                .Where(v => IsAlive(v.Checks)) //only include members that are alive
                                .Select(ToMember)
                                .ToArray();

                        //why is this not updated via the ClusterTopologyEvents?
                        //because following events is messy
                        _memberList.UpdateClusterTopology(currentMembers, waitIndex);
                        var res = new ClusterTopologyEvent(currentMembers);
                        _cluster.System.EventStream.Publish(res);
                    }
                }
            );

            Member ToMember(ServiceEntry v)
            {
                var member = new Member
                {
                    Id = v.Service.Meta["id"],
                    Host = v.Service.Address,
                    Port = v.Service.Port
                };

                member.Kinds.AddRange(v.Service.Tags);

                return member;
            }
        }

        private void StartUpdateTtlLoop() => _ = Task.Run(async () => {
                while (!_shutdown)
                {
                    await _client.Agent.PassTTL("service:" + _consulServiceInstanceId, "");
                    await Task.Delay(_refreshTtl, _cluster.System.Shutdown);
                }

                _logger.LogInformation("Exiting TTL loop");
            }
        );
        //
        // private void StartLeaderElectionLoop()
        // {
        //     _ = Task.Run(async () =>
        //         {
        //             try
        //             {
        //                 var leaderKey = $"{_consulServiceName}/leader";
        //                 var se = new SessionEntry
        //                 {
        //                     Behavior = SessionBehavior.Delete,
        //                     Name = leaderKey,
        //                     TTL = TimeSpan.FromSeconds(20)
        //                 };
        //                 var sessionRes = await _client.Session.Create(se);
        //                 var sessionId = sessionRes.Response;
        //
        //                 //this is used so that leader can update shared cluster state
        //                 _consulSessionId = sessionId;
        //                 _consulLeaderKey = leaderKey;
        //
        //                 var json = JsonConvert.SerializeObject(new ConsulLeader
        //                     {
        //                         Host = _host,
        //                         Port = _port,
        //                         MemberId = _cluster.Id
        //                     }
        //                 );
        //                 var kvp = new KVPair(leaderKey)
        //                 {
        //                     Key = leaderKey,
        //                     Session = sessionId,
        //                     Value = Encoding.UTF8.GetBytes(json)
        //                 };
        //
        //
        //                 //don't await this, it will block forever
        //                 _ = _client.Session.RenewPeriodic(TimeSpan.FromSeconds(1), sessionId, CancellationToken.None
        //                 );
        //
        //                 var waitIndex = 0ul;
        //                 while (!_shutdown && !_cluster.System.Shutdown.IsCancellationRequested)
        //                 {
        //                     try
        //                     {
        //                         var aquired = await _client.KV.Acquire(kvp,_cluster.System.Shutdown);
        //                         var isLeader = aquired.Response;
        //
        //                         var res = await _client.KV.Get(leaderKey, new QueryOptions
        //                             {
        //                                 Consistency = ConsistencyMode.Default,
        //                                 WaitIndex = waitIndex,
        //                                 WaitTime = TimeSpan.FromSeconds(3)
        //                             }
        //                         ,_cluster.System.Shutdown);
        //
        //
        //                         if (res.Response?.Value is null)
        //                         {
        //                             _logger.LogError("No leader info was found");
        //                             await Task.Delay(1000);
        //                             continue;
        //                         }
        //
        //                         var value = res.Response.Value;
        //                         var json2 = Encoding.UTF8.GetString(value);
        //                         var leader = JsonConvert.DeserializeObject<ConsulLeader>(json2);
        //                         waitIndex = res.LastIndex;
        //
        //                         var bannedMembers = Array.Empty<string>();
        //                         var banned = await _client.KV.Get(_consulServiceName + "/banned");
        //
        //                         if (banned.Response?.Value is not null)
        //                         {
        //                             var json3 = Encoding.UTF8.GetString(banned.Response.Value);
        //                             bannedMembers = JsonConvert.DeserializeObject<string[]>(json3);
        //                         }
        //
        //                         _memberList.UpdateLeader(new LeaderInfo(leader.MemberId, leader.Host, leader.Port,
        //                                 bannedMembers
        //                             )
        //                         );
        //                     }
        //                     catch (Exception x)
        //                     {
        //                         if (!_cluster.System.Shutdown.IsCancellationRequested)
        //                         {
        //                             _logger.LogError("Failed to read session data {x}", x);
        //                         }
        //                     }
        //                 }
        //
        //               //  await _client.KV.Release(kvp);
        //              //   await _client.Session.Destroy(sessionId);
        //             }
        //             catch (Exception x)
        //             {
        //                 _logger.LogCritical("Leader Election Failed {x}", x);
        //             }
        //         }
        //     );
        //}

        //register this cluster in consul.
        private async Task RegisterMemberAsync()
        {
            var s = new AgentServiceRegistration
            {
                ID = _consulServiceInstanceId,
                Name = _consulServiceName,
                Tags = _kinds.ToArray(),
                Address = _host,
                Port = _port,
                Check = new AgentServiceCheck
                {
                    DeregisterCriticalServiceAfter = _deregisterCritical,
                    TTL = _serviceTtl
                },
                Meta = new Dictionary<string, string>
                {
                    //register a unique ID for the current process
                    //if a node with host X and port Y, joins, then leaves, then joins again.
                    //we need a way to distinguish the new node from the old node.
                    //this is what this ID is for
                    {"id", _cluster.System.Id}
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

        private static bool IsAlive(HealthCheck[] serviceChecks) =>
            serviceChecks.All(c => c.Status == HealthStatus.Passing);
    }
}