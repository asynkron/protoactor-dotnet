using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
            var address = _partitionManager.Selector.GetIdentityOwner(msg.Name);

            if (!string.IsNullOrEmpty(address) && address != _cluster.System.ProcessRegistry.Address)
            {
                //if not, forward to the correct owner
                var owner = _partitionManager.RemotePartitionIdentityActor(address);
                _logger.LogDebug("Identity is not mine {Identity} forwarding to correct owner {Owner} ", msg.Name, owner
                );
                context.Send(owner, msg);
            }
            else
            {
                _logger.LogDebug("Taking Ownership of: {Name}, pid: {Pid}", msg.Name, msg.Pid);
                _partitionLookup[msg.Name] = (msg.Pid, msg.Kind);
                _reversePartition[msg.Pid] = msg.Name;
                context.Watch(msg.Pid);
            }
        }

        //removes any lookup to actors in a node that left
        //these actors are considered to be non existing now
        private void RemoveAddressFromPartition(string address)
        {
            foreach (var (actorId, info) in _partitionLookup.Where(x => x.Value.pid.Address == address).ToArray())
            {
                _partitionLookup.Remove(actorId);
                _reversePartition.Remove(info.pid);
            }
        }


        private void ClearInvalidOwnership(IContext context)
        {
            var transferredActorCount = 0;
            //loop over all identities we own, if we are no longer the algorithmic owner, clear ownership

            var myAddress = context.Self.Address;

            foreach (var (identity, (pid, kind)) in _partitionLookup.ToArray())
            {
                var shouldBeOwnerAddress = _partitionManager.Selector.GetIdentityOwner(identity);

                if (shouldBeOwnerAddress == myAddress)
                {
                    continue;
                }

                transferredActorCount++;
                _partitionLookup.Remove(identity);
                _reversePartition.Remove(pid);
                context.Unwatch(pid);
            }

            if (transferredActorCount > 0)
            {
                _logger.LogInformation("Transferred {TransferCount} PIDs to other nodes", transferredActorCount);
            }
        }

        private void MemberLeft(MemberLeftEvent msg, IContext context)
        {
            //always do this when a member leaves, we need to redistribute the distributed-hash-table
            //no ifs or else, just always
            ClearInvalidOwnership(context);

            RemoveAddressFromPartition(msg.Member.Address);
        }

        private void MemberJoined(MemberJoinedEvent msg, IContext context)
        {
            ClearInvalidOwnership(context);
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
            var activatorAddress = _cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activatorAddress))
            {
                //No activator currently available, return unavailable
                _logger.LogWarning("[Partition] No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            var spawning = SpawnRemoteActor(msg, activatorAddress);

            //Await SpawningProcess
            context.ReenterAfter(
                spawning,
                rst =>
                {
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
                    return Actor.Done;
                }
            );
        }

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
        private async Task<ActorPidResponse> SpawnNamedAsync(string address, string name, string kind, TimeSpan timeout)
        {
            var activator = _partitionManager.RemotePartitionPlacementActor(address);

            var res = await _cluster.System.Root.RequestAsync<ActorPidResponse>(
                activator, new ActorPidRequest
                {
                    Kind = kind,
                    Name = name
                }, timeout
            );

            return res;
        }
    }
}