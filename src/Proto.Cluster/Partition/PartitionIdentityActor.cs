// -----------------------------------------------------------------------
// <copyright file="PartitionIdentityActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

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

        private readonly Rendezvous _rdv = new();

        private readonly Dictionary<ClusterIdentity, Task<ActivationResponse>> _spawns = new();

        private ulong _topologyHash;
        private readonly TimeSpan _identityHandoverTimeout;

        public PartitionIdentityActor(Cluster cluster, TimeSpan identityHandoverTimeout)
        {
            _cluster = cluster;
            _myAddress = cluster.System.Address;
            _identityHandoverTimeout = identityHandoverTimeout;
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
            var self = context.Self!;
            _cluster.System.EventStream.Subscribe<ActivationTerminated>(e => {
                
                _cluster.System.Root.Send(self,e);
            });
            
            return Task.CompletedTask;
        }

        private async Task OnClusterTopology(ClusterTopology msg, IContext context)
        {
            await _cluster.MemberList.TopologyConsensus();
            if (_topologyHash == msg.TopologyHash) return;

            _topologyHash = msg.TopologyHash;
            var members = msg.Members.ToArray();

            _rdv.UpdateMembers(members);

            //remove all identities we do no longer own.
            _partitionLookup.Clear();

            var requests = new List<Task<IdentityHandoverResponse>>();
            var requestMsg = new IdentityHandoverRequest
            {
                TopologyHash = _topologyHash,
                Address = _myAddress
            };

            requestMsg.Members.AddRange(members);

            foreach (var member in members)
            {
                var activatorPid = PartitionManager.RemotePartitionPlacementActor(member.Address);
                var request =
                    context.RequestAsync<IdentityHandoverResponse>(activatorPid, requestMsg, CancellationTokens.WithTimeout(_identityHandoverTimeout));
                requests.Add(request);
            }

            try
            {
                Logger.LogDebug("Requesting ownerships");

                //built in timeout on each request above
                var responses = await Task.WhenAll(requests);
                Logger.LogDebug("Got ownerships {EventId}", _topologyHash);

                foreach (var response in responses)
                {
                    foreach (var actor in response.Actors)
                    {
                        TakeOwnership(actor);

                        if (!_partitionLookup.ContainsKey(actor.ClusterIdentity))
                            Logger.LogError("Ownership bug, we should own {Identity}", actor.ClusterIdentity);
                        else
                            Logger.LogDebug("I have ownership of {Identity}", actor.ClusterIdentity);
                    }
                }
            }
            catch (Exception x)
            {
                Logger.LogError(x, "Failed to get identities");
            }

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

        private void TakeOwnership(Activation msg)
        {
            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var existing))
            {
                //these are the same, that's good, just ignore message
                if (existing.Address == msg.Pid.Address) return;
            }

            Logger.LogDebug("Taking Ownership of: {Identity}, pid: {Pid}", msg.Identity, msg.Pid);
            _partitionLookup[msg.ClusterIdentity] = msg.Pid;
        }

        private async Task OnActivationRequest(ActivationRequest msg, IContext context)
        {
            var ownerAddress = _rdv.GetOwnerMemberByIdentity(msg.Identity);

            if (ownerAddress != _myAddress)
            {
                var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
                Logger.LogWarning("Tried to spawn on wrong node, forwarding");
                context.Forward(ownerPid);

                return;
            }

            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out var pid))
            {
                if (pid == null)
                {
                    Logger.LogError("Null PID for ClusterIdentity {ClusterIdentity}",msg.ClusterIdentity);
                }
                context.Respond(new ActivationResponse {Pid = pid});
                return;
            }
            
            //only activate members when we are all in sync
            await _cluster.MemberList.TopologyConsensus();

            //Get activator
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind, context.Sender!.Address)?.Address;

            //just make the code analyzer understand the address is not null after this block
            if (activatorAddress is null || string.IsNullOrEmpty(activatorAddress))
            {
                //No activator currently available, return unavailable
                Logger.LogWarning("No members currently available for kind {Kind}", msg.Kind);
                context.Respond(new ActivationResponse {Pid = null});
                return;
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

            //Await SpawningProcess
            context.ReenterAfter(
                res,
                rst => {


                    var response = res.Result;

                    //TODO: as this is async, there might come in multiple ActivationRequests asking for this
                    //Identity, causing multiple activations

                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfer.
                    if (_partitionLookup.TryGetValue(msg.ClusterIdentity, out pid))
                    {
                        _spawns.Remove(msg.ClusterIdentity);
                        context.Respond(new ActivationResponse {Pid = pid});
                        return Task.CompletedTask;
                    }

                    //Check if process is faulted
                    if (rst.IsFaulted)
                    {
                        _spawns.Remove(msg.ClusterIdentity);
                        context.Respond(response);
                        return Task.CompletedTask;
                    }
                    if (response == null)
                    {
                        _spawns.Remove(msg.ClusterIdentity);
                        // context.Respond(new ActivationResponse()
                        // {
                        //     
                        // });
                        //TODO what do we do in this case?
                        return Task.CompletedTask;
                    }

                    _partitionLookup[msg.ClusterIdentity] = response.Pid;
                    context.Respond(response);

                    try
                    {
                        _spawns.Remove(msg.ClusterIdentity);
                    }
                    catch (Exception e)
                    {
                        //debugging hack
                        Logger.LogInformation(e, "Failed while removing spawn {Id}", msg.Identity);
                    }

                    return Task.CompletedTask;
                }
            );
        }

        private async Task<ActivationResponse> SpawnRemoteActor(ActivationRequest req, string activator)
        {
            try
            {
                Logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activator, req.Identity,
                    req.Kind
                );
                var timeout = _cluster.Config!.TimeoutTimespan;
                var activator1 = PartitionManager.RemotePartitionPlacementActor(activator);

                var res = await _cluster.System.Root.RequestAsync<ActivationResponse>(activator1, req, timeout);
                return res;
            }
            catch
            {
                return null!;
            }
        }
    }
}