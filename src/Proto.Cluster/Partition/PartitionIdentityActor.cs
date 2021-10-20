// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Utils;

namespace Proto.Cluster.Partition
{
    //This actor is responsible to keep track of identities owned by this member
    //it does not manage the cluster spawned actors itself, only identity->remote PID management
    //TLDR; this is a partition/bucket in the distributed hash table which makes up the identity lookup
    //
    //for spawning/activating cluster actors see PartitionActivator.cs
    class PartitionIdentityActor : IActor
    {
        //for how long do we wait when performing a identity handover?
        
        private readonly Cluster _cluster;
        private static readonly ILogger Logger = Log.CreateLogger<PartitionIdentityActor>();
        private readonly string _myAddress;

        private readonly Dictionary<ClusterIdentity, PID> _partitionLookup = new(); //actor/grain name to PID

        private MemberHashRing _rdv = new(ImmutableList<Member>.Empty);

        private readonly Dictionary<ClusterIdentity, Task<ActivationResponse>> _spawns = new();

        private ulong _topologyHash;
        private readonly TimeSpan _identityHandoverTimeout;
        private readonly PartitionConfig _config;

        public PartitionIdentityActor(Cluster cluster, TimeSpan identityHandoverTimeout, PartitionConfig config)
        {
            _cluster = cluster;
            _myAddress = cluster.System.Address;
            _identityHandoverTimeout = identityHandoverTimeout;
            _config = config;
        }
        
        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started                  => OnStarted(context),
                ActivationRequest msg    => OnActivationRequest(msg, context),
                ActivationTerminated msg => OnActivationTerminated(msg, context),
                ClusterTopology msg      => OnClusterTopology(msg, context),
                _                        => Task.CompletedTask
            };

        private Task OnStarted(IContext context)
        {
            var self = context.Self;
            _cluster.System.EventStream.Subscribe<ActivationTerminated>(e => {
                
                _cluster.System.Root.Send(self,e);
            });
            
            return Task.CompletedTask;
        }

        private async Task OnClusterTopology(ClusterTopology msg, IContext context)
        {
            await Retry.Try(() => OnClusterTopologyInner(msg, context), onError: OnError, onFailed: OnFailed, ignoreFailure:true);

            static void OnError(int attempt, Exception exception) => Logger.LogWarning(exception, "Failed to handle topology change");

            static void OnFailed(Exception exception) => Logger.LogError(exception, "Failed to handle topology change");
        }

        private async Task OnClusterTopologyInner(ClusterTopology msg, IContext context)
        {
       //     await _cluster.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));
            var members = msg.Members.ToArray();
            _topologyHash = msg.TopologyHash;
            _rdv = new MemberHashRing(msg.Members);

            //remove all identities we do no longer own.
            _partitionLookup.Clear();

            var requestMsg = new IdentityHandoverRequest
            {
                TopologyHash = _topologyHash,
                Address = _myAddress
            };

            requestMsg.Members.AddRange(members);

            var workerPid = context.Spawn(Props.FromProducer(() => new PartitionIdentityRelocationWorker(_partitionLookup)));
            using var cts = new CancellationTokenSource(_identityHandoverTimeout);
            Logger.LogDebug("Requesting ownerships");
            var response = await context.RequestAsync<IdentityHandoverAcknowledgement>(workerPid,requestMsg, cts.Token);
            
            Logger.LogDebug("Got ownerships {EventId}, {Count}", _topologyHash, response.ChunkId);
            
            var membersLookup = msg.Members.ToDictionary(m => m.Address, m => m);

            //scan through all id lookups and remove cases where the address is no longer part of cluster members
            foreach (var (actorId, pid) in _partitionLookup.ToArray())
            {
                if (!membersLookup.ContainsKey(pid.Address)) _partitionLookup.Remove(actorId);
            }
        }

        private Task OnActivationTerminated(ActivationTerminated msg, IContext context)
        {
            if (_spawns.ContainsKey(msg.ClusterIdentity))
            {
                return Task.CompletedTask;
            }
            //we get this via broadcast to all nodes, remove if we have it, or ignore
            Logger.LogDebug("[PartitionIdentityActor] Terminated {Pid}", msg.Pid);
           // _cluster.PidCache.RemoveByVal(msg.ClusterIdentity,msg.Pid);
            _partitionLookup.Remove(msg.ClusterIdentity);

            return Task.CompletedTask;
        }



        private Task OnActivationRequest(ActivationRequest msg, IContext context)
        {
            if (_config.DeveloperLogging)
                Console.WriteLine($"Got ActivationRequest {msg.RequestId}");
            
            var ownerAddress = _rdv.GetOwnerMemberByIdentity(msg.Identity);

            if (ownerAddress != _myAddress)
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"Forwarding ActivationRequest {msg.RequestId} to {ownerAddress}");
                
                var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
                Logger.LogWarning("Tried to spawn on wrong node, forwarding");
                context.Forward(ownerPid);

                return Task.CompletedTask;
            }

            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var pid))
            {
                if (_config.DeveloperLogging)
                    Console.WriteLine($"Found existing activation for {msg.RequestId}");
                
                if (pid == null)
                {
                    if (_config.DeveloperLogging)
                        Console.WriteLine($"Found null activation for {msg.RequestId}");
                    
                    _partitionLookup.Remove(msg.ClusterIdentity);
                    Logger.LogError("Null PID for ClusterIdentity {ClusterIdentity}",msg.ClusterIdentity);
                    context.Respond(new ActivationResponse()
                    {
                        Failed = true,
                    });
                    return Task.CompletedTask;
                }
                context.Respond(new ActivationResponse {Pid = pid});
                return Task.CompletedTask;
            }
            
            //only activate members when we are all in sync
            // var c = await _cluster.MemberList.TopologyConsensus(CancellationTokens.FromSeconds(5));
            //
            // if (!c)
            // {
            //     Console.WriteLine("No consensus " + _cluster.System.Id);
            // }

            //Get activator
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

            if (string.IsNullOrEmpty(activatorAddress))
            {
                if (_config.DeveloperLogging)
                    Console.Write("?");
                //No activator currently available, return unavailable
                Logger.LogWarning("No members currently available for kind {Kind}", msg.Kind);
                context.Respond(new ActivationResponse
                {
                    Failed = true
                });
                return Task.CompletedTask;
            }

            //What is this?
            //in case the actor of msg.Name is not yet spawned. there could be multiple re-entrant
            //messages requesting it, we just reuse the same task for all those
            //once spawned, the key is removed from this dict
            if (!_spawns.TryGetValue(msg.ClusterIdentity, out var res))
            {
                res = SpawnRemoteActor(msg, activatorAddress);
                _spawns.Add(msg.ClusterIdentity, res);
            }

            //execution ends here. context.ReenterAfter is invoked once the task completes
            //but still within the actors sequential execution
            //but other messages could have been processed in between

            if (_config.DeveloperLogging)
                Console.Write("S"); //spawned
            //Await SpawningProcess
            context.ReenterAfter(
                res,
                async rst => {
                    try
                    {
                        var response = await rst;
                        if (_config.DeveloperLogging)
                            Console.Write("R"); //reentered
                        
                        if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out pid))
                        {
                            if (_config.DeveloperLogging)
                                Console.Write("C");  //cached
                            _spawns.Remove(msg.ClusterIdentity);
                            context.Respond(new ActivationResponse {Pid = pid});
                            return;
                        }

                        if (response?.Pid != null)
                        {
                            if (_config.DeveloperLogging)
                                Console.Write("A"); //activated
                            _partitionLookup[msg.ClusterIdentity] = response.Pid;
                            _spawns.Remove(msg.ClusterIdentity);
                            context.Respond(response);
                            return;
                        }
                    }
                    catch(Exception x)
                    {
                        Logger.LogError(x, "Spawning failed");
                    }
                    
                    if (_config.DeveloperLogging)
                        Console.Write("F"); //failed
                    _spawns.Remove(msg.ClusterIdentity);
                    context.Respond(new ActivationResponse
                    {
                        Failed = true
                    });
                }
            );
            return Task.CompletedTask;
        }

        private async Task<ActivationResponse> SpawnRemoteActor(ActivationRequest req, string activatorAddress)
        {
            try
            {
                Logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activatorAddress, req.Identity,
                    req.Kind
                );
                var timeout = _cluster.Config.TimeoutTimespan;
                var activatorPid = PartitionManager.RemotePartitionPlacementActor(activatorAddress);

                var res = await _cluster.System.Root.RequestAsync<ActivationResponse>(activatorPid, req, timeout);
                return res;
            }
            catch
            {
                return new ActivationResponse()
                {
                    Failed = true
                };
            }
        }
    }
}