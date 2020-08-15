using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    internal class PartitionActor : IActor
    {
        private readonly ILogger _logger;
        private readonly Dictionary<string, (PID pid, string kind)> _partitionLookup = new Dictionary<string, (PID pid, string kind)>();        //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name

        // private readonly Partition _partition;

        private readonly PartitionManager _partitionManager;
        private readonly Cluster _cluster;

        public PartitionActor(Cluster cluster, PartitionManager partitionManager)
        {
            _logger = Log.CreateLogger("PartitionActor-" + cluster.Id);
            _cluster = cluster;
            _partitionManager = partitionManager;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
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
                case MemberJoinedEvent msg:
                    MemberJoined(msg, context);
                    break;
                case MemberLeftEvent msg:
                    MemberLeft(msg, context);
                    break;
            }

            return Actor.Done;
        }

        private void Terminated(Terminated msg)
        {
            //one of the actors we manage died, remove it from the lookup
            if (_reversePartition.TryGetValue(msg.Who, out var key))
            {
                _partitionLookup.Remove(key);
                _reversePartition.Remove(msg.Who);
            }
        }

        private void TakeOwnership(TakeOwnership msg, IContext context)
        {
            //Check again if I'm still the owner of the identity
            var address = _cluster.MemberList.GetMemberFromIdentityAndKind(msg.Name, msg.Kind);

            if (!string.IsNullOrEmpty(address) && address != _cluster.System.ProcessRegistry.Address)
            {
                //if not, forward to the correct owner
                var owner = _partitionManager.RemotePartitionForKind(address);
                _logger.LogError("Identity is not mine {Identity} forwarding to correct owner {Owner} ", msg.Name, owner);
                context.Send(owner, msg);
            }
            else
            {
                _logger.LogError("Kind ??? Take Ownership name: {Name}, pid: {Pid}", msg.Name, msg.Pid);
                _partitionLookup[msg.Name] = (msg.Pid,msg.Kind);
                _reversePartition[msg.Pid] = msg.Name;
                context.Watch(msg.Pid);
            }
        }

        private void RemoveAddressFromPartition(string address)
        {
            foreach (var (actorId, info) in _partitionLookup.Where(x => x.Value.pid.Address == address).ToArray())
            {
                _partitionLookup.Remove(actorId);
                _reversePartition.Remove(info.pid);
            }
        }
        

        private void EvaluateAndTransferOwnership(IContext context)
        {
            // Iterate through the actors in this partition and try to check if the partition
            // PID should be in is not the current one, if so initiates a transfer to the
            // new partition.
            var transferredActorCount = 0;

            // TODO: right now we transfer ownership on a per actor basis.
            // this could be done in a batch
            // ownership is also racy, new nodes should maybe forward requests to neighbours (?)
            foreach (var (actorId, info) in _partitionLookup.ToArray())
            {
                var address = _cluster.MemberList.GetMemberFromIdentityAndKind(actorId,info.kind);

                if (!string.IsNullOrEmpty(address) && address != _cluster.System.ProcessRegistry.Address)
                {
                    transferredActorCount++;
                    TransferOwnershipOfActor(actorId, info.kind,info.pid, address, context);
                }
            }

            if (transferredActorCount > 0)
            {
                _logger.LogInformation($"Transferred {transferredActorCount} PIDs to other nodes");
            }
            
        }

        private void MemberLeft(MemberLeftEvent memberLeft, IContext context)
        {
            //always do this when a member leaves, we need to redistribute the distributed-hash-table
            //no ifs or elses, just always
            EvaluateAndTransferOwnership(context);

            RemoveAddressFromPartition(memberLeft.Address);

        }

        private void MemberJoined(MemberJoinedEvent msg, IContext context)
        {
            EvaluateAndTransferOwnership(context);
        }

        private void TransferOwnershipOfActor(string actorId, string kind, PID pid, string address, IContext context)
        {
            var owner = _partitionManager.RemotePartitionForKind(address);
            context.Send(owner, new TakeOwnership {Name = actorId,Kind = kind, Pid = pid});
            _partitionLookup.Remove(actorId);
            _reversePartition.Remove(pid);
            context.Unwatch(pid);
        }

        private void GetOrSpawn(ActorPidRequest msg, IContext context)
        {
            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.Name, out var info))
            {
                context.Respond(new ActorPidResponse {Pid = info.pid});
                return;
            }
            
            //Get activator
            var activator = _cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activator))
            {
                //No activator currently available, return unavailable
                _logger.LogWarning("[Partition] No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            var spawning = SpawnRemoteActor(msg, activator);
            
            //Await SpawningProcess
            context.ReenterAfter(
                spawning,
                rst =>
                {
                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfering.
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

                        _partitionLookup[msg.Name] = (pid,msg.Kind);
                        _reversePartition[pid] = msg.Name;
                        context.Watch(pid);
                    }

                    context.Respond(pidResp);
                    return Actor.Done;
                }
            );
        }

        private async Task<ActorPidResponse> SpawnRemoteActor(ActorPidRequest req, string activator)
        {
            try
            {
                _logger.LogDebug("Spawning Remote Actor {Activator} {Identity} {Kind}", activator,req.Name,req.Kind);
                return await _cluster.Remote.SpawnNamedAsync(activator, req.Name, req.Kind, _cluster.Config!.TimeoutTimespan);
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
    }
}