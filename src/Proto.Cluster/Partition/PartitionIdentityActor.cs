using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Events;
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    //This actor is responsible to keep track of identities owned by this member
    //it does not manage the cluster spawned actors itself, only identity->remote PID management
    //TLDR; this is a partition/bucket in the distributed hash table which makes up the identity lookup
    //
    //for spawning/activating cluster actors see PartitionActivator.cs
    internal class PartitionIdentityActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        private readonly Dictionary<string, (PID pid, string kind)> _partitionLookup =
            new Dictionary<string, (PID pid, string kind)>(); //actor/grain name to PID

        private readonly PartitionManager _partitionManager;
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name
        private ulong _eventId;
        private DateTime _lastEventTimestamp;


        public PartitionIdentityActor(Cluster cluster, PartitionManager partitionManager)
        {
            _logger = Log.CreateLogger($"{nameof(PartitionIdentityActor)}-{cluster.Id}");
            _cluster = cluster;
            _partitionManager = partitionManager;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _lastEventTimestamp = DateTime.Now;
                    _logger.LogDebug("Started");
                    break;
                case ActorPidRequest msg:
                    GetOrSpawn(msg, context);
                    break;
                case Terminated msg:
                    Terminated(msg);
                    break;
                case TakeOwnership msg:
                    TakeOwnership(msg, context);
                    break;

                case ClusterTopology msg:
                    if (_eventId < msg.EventId)
                    {
                        _eventId = msg.EventId;
                        _lastEventTimestamp = DateTime.Now;
                        _logger.LogWarning("--- Topology change --- {EventId} --- pausing interactions for 1 sec ---",_eventId);
                        ClusterTopology(msg, context);
                    }

                    break;
            }

            return Actor.Done;
        }

        public void ClusterTopology(ClusterTopology msg, IContext context)
        {
            //always do this when a member leaves, we need to redistribute the distributed-hash-table
            //no ifs or else, just always
            //ClearInvalidOwnership(context);

            var members = msg.Members.ToDictionary(m => m.Address, m => m);
            
            //scan through all id lookups and remove cases where the address is no longer part of cluster members
            foreach (var (actorId, (pid, _)) in _partitionLookup.ToArray())
            {
                if (!members.ContainsKey(pid.Address))
                {
                    _partitionLookup.Remove(actorId);
                    _reversePartition.Remove(pid);
                }
            }

        }

        private void Terminated(Terminated msg)
        {
            //one of the actors we manage died, remove it from the lookup
            if (_reversePartition.TryGetValue(msg.Who, out var key))
            {
                _logger.LogDebug("Terminated {Pid}",msg.Who);
                _partitionLookup.Remove(key);
                _reversePartition.Remove(msg.Who);
            }
        }

        private void TakeOwnership(TakeOwnership msg, IContext context)
        {
            //Check again if I'm still the owner of the identity
            var address = _partitionManager.Selector.GetIdentityOwner(msg.Name);

            
            if (!string.IsNullOrEmpty(address) && address != _cluster.System.ProcessRegistry.Address)
            {
                //after live testing, this strategy works extremely well. 
                //if not, forward to the correct owner
                var owner = _partitionManager.RemotePartitionIdentityActor(address);
                _logger.LogDebug("Identity '{Identity}' is not mine {Self}, forwarding to correct owner {Owner} ", msg.Name,context.Self, owner);
                context.Send(owner, msg);
            }
            else
            {
                if (_partitionLookup.TryGetValue(msg.Name,out var existing))
                {
                    //these are the same, that's good, just ignore message
                    //should not really be possible, but let's guard against it..
                    if (existing.pid.Address == msg.Pid.Address)
                    {
                        _logger.LogDebug("Received TakeOwnership message but already knows about this Identity {Identity} {Pid}",msg.Name,existing.pid);
                        return;
                    }
                    
                    //TODO:
                    //this is tricky, if we have two duplicate activations, both are telling this node to take ownership 
                    //we need an idempotent way to decide which one to keep, so we dont end up stopping both 
                    //
                    //one easy approach is to always keep the one with the lowest or highest address ordinal

                    // if (String.CompareOrdinal(existing.pid.Address, msg.Pid.Address) < 0)
                    // {
                        _logger.LogWarning("Duplicate activation detected for Identity '{Identity}', Known {KnownPid}, Other {OtherPid}, Stopping Other",msg.Name,existing.pid,msg.Pid);
                    
                        //kill duplicate activation...
                        _cluster.System.Root.Stop(msg.Pid);
                    // }
                    // else
                    // {
                    //     _logger.LogWarning("Duplicate activation detected for Identity '{Identity}', Known {KnownPid}, Other {OtherPid}, Stopping Known",msg.Name,existing.pid,msg.Pid);
                    //
                    //     _partitionLookup[msg.Name] = (msg.Pid, msg.Kind);
                    //     _reversePartition[msg.Pid] = msg.Name;
                    //     context.Watch(msg.Pid);
                    //     //kill duplicate activation...
                    //     _cluster.System.Root.Stop(existing.pid);
                    // }
                }
                else
                {
                    _logger.LogInformation("Taking Ownership of: {Identity}, pid: {Pid}", msg.Name, msg.Pid);
                    _partitionLookup[msg.Name] = (msg.Pid, msg.Kind);
                    _reversePartition[msg.Pid] = msg.Name;
                    context.Watch(msg.Pid);
                }
            }
        }


        private void ClearInvalidOwnership(IContext context)
        {
            //loop over all identities we own, if we are no longer the algorithmic owner, clear ownership

            var myAddress = context.Self!.Address;

            foreach (var (identity, (pid, _)) in _partitionLookup.ToArray())
            {
                var shouldBeOwnerAddress = _partitionManager.Selector.GetIdentityOwner(identity);

                if (shouldBeOwnerAddress == myAddress)
                {
                    continue;
                }
                _partitionLookup.Remove(identity);
                _reversePartition.Remove(pid);
                context.Unwatch(pid);
            }
        }

        private void GetOrSpawn(ActorPidRequest msg, IContext context)
        {

            
            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.Name, out var info))
            {
                context.Respond(new ActorPidResponse {Pid = info.pid});
                return;
            }
            
            if (SendLater(msg, context))
            {
                return;
            }

            //Get activator
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activatorAddress))
            {
                //No activator currently available, return unavailable
                _logger.LogWarning("[Partition] No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            //What is this?
            //incase the actor of msg.Name is not yet spawned. there could be multiple reentrant
            //messages requesting it, we just reuse the same task for all those
            //once spawned, the key is removed from this dict
            if (!_spawns.TryGetValue(msg.Name, out var res))
            {
                res = SpawnRemoteActor(msg, activatorAddress);
                _spawns.Add(msg.Name, res);
            }

            //Await SpawningProcess
            context.ReenterAfter(
                res,
                rst =>
                {
                    //TODO: as this is async, there might come in multiple ActorPidRequests asking for this
                    //Identity, causing multiple activations
                    
                    
                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfer.
                    if (_partitionLookup.TryGetValue(msg.Name, out info))
                    {
                        context.Respond(new ActorPidResponse {Pid = info.pid});
                        return Actor.Done;
                    }

                    //Check if process is faulted
                    if (rst.IsFaulted)
                    {
                        context.Respond(ActorPidResponse.Err);
                        return Actor.Done;
                    }

                    var pidResp = rst.Result;

                    if ((ResponseStatusCode) pidResp.StatusCode == ResponseStatusCode.OK)
                    {
                        var pid = pidResp.Pid;

                        _partitionLookup[msg.Name] = (pid, msg.Kind);
                        _reversePartition[pid] = msg.Name;
                        context.Watch(pid);
                    }

                    context.Respond(pidResp);
                    _spawns.Remove(msg.Name);
                    return Actor.Done;
                }
            );
        }

        private bool SendLater(object msg, IContext context)
        {
            //TODO: buffer this in a queue and consume once we are past timestamp
            if (DateTime.Now <= _lastEventTimestamp.AddSeconds(1))
            {
                var self = context.Self;
                Task.Delay(100).ContinueWith(t => { _cluster.System.Root.Send(self, msg); });
                return true;
            }

            return false;
        }

        private Dictionary<string, Task<ActorPidResponse>> _spawns = new Dictionary<string, Task<ActorPidResponse>>();
        private async Task<ActorPidResponse> SpawnRemoteActor(ActorPidRequest req, string activator)
        {
            try
            {
                _logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activator, req.Name, req.Kind);
                return await SpawnNamedAsync(activator, req.Name, req.Kind, _cluster.Config!.TimeoutTimespan);
            }
            catch (TimeoutException)
            {
                return ActorPidResponse.TimeOut;
            }
            catch
            {
                return ActorPidResponse.Err;
            }
        }

        //identical to Remote.SpawnNamedAsync, just using the special partition-activator for spawning
        private async Task<ActorPidResponse> SpawnNamedAsync(string address, string identity, string kind, TimeSpan timeout)
        {
            var activator = _partitionManager.RemotePartitionPlacementActor(address);

            var eventId = _eventId;
            var res = await _cluster.System.Root.RequestAsync<ActorPidResponse>(
                activator, new ActorPidRequest
                {
                    Kind = kind,
                    Name = identity
                }, timeout
            );
            if (_partitionLookup.TryGetValue(identity,out var existing))
            {
                _logger.LogWarning("Spawned remote actor but got ownership of other during the time {Identity}", identity);

                if (res.Pid != null)
                {
                    //kill newly spawned actor
                    _cluster.System.Root.Send(res.Pid,Stop.Instance);
                }

                return new ActorPidResponse
                {
                    Pid = existing.pid,
                    StatusCode = (int) ResponseStatusCode.OK
                };
            }

            return res;
        }
    }
}