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
        private static readonly ILogger Logger = Log.CreateLogger<PartitionActor>();

        private class SpawningProcess : TaskCompletionSource<ActorPidResponse>
        {
            public string SpawningAddress { get; }
            public SpawningProcess(string address) => SpawningAddress = address;
        }

        private readonly string _kind;
        private readonly Dictionary<string, PID> _partitionLookup = new Dictionary<string, PID>();        //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name

        // private readonly Partition _partition;
        private readonly Dictionary<string, SpawningProcess> _spawningProcs = new Dictionary<string, SpawningProcess>(); //spawning processes
        private Partition _partition;
        public Cluster Cluster { get; }

        public PartitionActor(Cluster cluster, string kind, Partition partition)
        {
            Cluster = cluster;
            _kind = kind; 
            _partition = partition;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    Logger.LogDebug("[Partition] Started for {Kind}", _kind);
                    break;
                case ActorPidRequest msg:
                    Spawn(msg, context);
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
                case MemberRejoinedEvent msg:
                    MemberRejoined(msg);
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
            //Check again if I'm the owner
            var address = Cluster.MemberList.GetPartition(msg.Name, _kind);

            if (!string.IsNullOrEmpty(address) && address != Cluster.System.ProcessRegistry.Address)
            {
                //if not, forward to the correct owner
                var owner = _partition.PartitionForKind(address, _kind);

                context.Send(owner, msg);
            }
            else
            {
                Logger.LogDebug("[Partition] Kind {Kind} Take Ownership name: {Name}, pid: {Pid}", _kind, msg.Name, msg.Pid);
                _partitionLookup[msg.Name] = msg.Pid;
                _reversePartition[msg.Pid] = msg.Name;
                context.Watch(msg.Pid);
            }
        }

        private void RemoveAddressFromPartition(string address)
        {
            foreach (var (actorId, pid) in _partitionLookup.Where(x => x.Value.Address == address))
            {
                _partitionLookup.Remove(actorId);
                _reversePartition.Remove(pid);
            }
        }

        private void MakeUnavailable(string address)
        {
            foreach (var (_, sp) in _spawningProcs.Where(x => x.Value.SpawningAddress == address))
            {
                sp.TrySetResult(ActorPidResponse.Unavailable);
            }
        }

        private int TransferOwnership(IContext context)
        {
            // Iterate through the actors in this partition and try to check if the partition
            // PID should be in is not the current one, if so initiates a transfer to the
            // new partition.
            var transferredActorCount = 0;

            // TODO: right now we transfer ownership on a per actor basis.
            // this could be done in a batch
            // ownership is also racy, new nodes should maybe forward requests to neighbours (?)
            foreach (var (actorId, _) in _partitionLookup.ToArray())
            {
                var address = Cluster.MemberList.GetPartition(actorId, _kind);

                if (!string.IsNullOrEmpty(address) && address != Cluster.System.ProcessRegistry.Address)
                {
                    transferredActorCount++;
                    TransferOwnership(actorId, address, context);
                }
            }

            return transferredActorCount;
        }

        private void MemberLeft(MemberLeftEvent memberLeft, IContext context)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member left {Address}", _kind, memberLeft.Address);

            // If the left member is self, transfer remaining pids to others
            if (memberLeft.Address == Cluster.System.ProcessRegistry.Address)
            {
                var transferredActorCount = TransferOwnership(context);

                if (transferredActorCount > 0) Logger.LogInformation("[Partition] Transferred {actors} PIDs to other nodes", transferredActorCount);
            }

            RemoveAddressFromPartition(memberLeft.Address);

            // Process Spawning Process
            MakeUnavailable(memberLeft.Address);
        }

        private void MemberRejoined(MemberRejoinedEvent memberRejoined)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member rejoined {Address}", _kind, memberRejoined.Address);

            RemoveAddressFromPartition(memberRejoined.Address);

            // Process Spawning Process
            MakeUnavailable(memberRejoined.Address);
        }

        private void MemberJoined(MemberJoinedEvent msg, IContext context)
        {
            Logger.LogInformation("[Partition] Kind {Kind} member joined {Address}", _kind, msg.Address);

            var transferredActorCount = TransferOwnership(context);

            if (transferredActorCount > 0) Logger.LogInformation("[Partition] Transferred {actors} PIDs to other nodes", transferredActorCount);

            foreach (var (actorId, sp) in _spawningProcs)
            {
                var address = Cluster.MemberList.GetPartition(actorId, _kind);

                if (address != Cluster.System.ProcessRegistry.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void TransferOwnership(string actorId, string address, IContext context)
        {
            var pid = _partitionLookup[actorId];
            var owner = _partition.PartitionForKind(address, _kind);
            context.Send(owner, new TakeOwnership {Name = actorId, Pid = pid});
            _partitionLookup.Remove(actorId);
            _reversePartition.Remove(pid);
            context.Unwatch(pid);
        }

        private void Spawn(ActorPidRequest msg, IContext context)
        {
            //Check if exist in current partition dictionary
            if (_partitionLookup.TryGetValue(msg.Name, out var pid))
            {
                context.Respond(new ActorPidResponse {Pid = pid});
                return;
            }

            //Check if is spawning, if so just await spawning finish.
            if (_spawningProcs.TryGetValue(msg.Name, out var spawning))
            {
                context.ReenterAfter(
                    spawning.Task,
                    rst =>
                    {
                        context.Respond(rst.IsFaulted ? ActorPidResponse.Err : rst.Result);
                        return Actor.Done;
                    }
                );
                return;
            }

            //Get activator
            var activator = Cluster.MemberList.GetActivator(msg.Kind);

            if (string.IsNullOrEmpty(activator))
            {
                //No activator currently available, return unavailable
                Logger.LogDebug("[Partition] No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            //Create SpawningProcess and cache it in spawning dictionary.
            spawning = new SpawningProcess(activator);
            _spawningProcs[msg.Name] = spawning;

            //Await SpawningProcess
            context.ReenterAfter(
                spawning.Task,
                rst =>
                {
                    _spawningProcs.Remove(msg.Name);

                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfering.
                    if (_partitionLookup.TryGetValue(msg.Name, out pid))
                    {
                        context.Respond(new ActorPidResponse {Pid = pid});
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
                        pid = pidResp.Pid;
                        _partitionLookup[msg.Name] = pid;
                        _reversePartition[pid] = msg.Name;
                        context.Watch(pid);
                    }

                    context.Respond(pidResp);
                    return Actor.Done;
                }
            );

            //Perform Spawning
            Task.Factory.StartNew(() => Spawning(msg, activator, 3, spawning));
        }

        private async Task Spawning(ActorPidRequest req, string activator, int retryCount, SpawningProcess spawning)
        {
            var retry = retryCount;
            ActorPidResponse pidResp;

            //TODO: add backoff interval
            do
            {
                //TODO: await Task.Delay( something something retrycount ...)
                pidResp = await TrySpawn(retry == retryCount ? activator : Cluster.MemberList.GetActivator(req.Kind));
            } while ((ResponseStatusCode) pidResp.StatusCode == ResponseStatusCode.Unavailable && retry-- > 0);

            spawning.TrySetResult(pidResp);

            async Task<ActorPidResponse> TrySpawn(string act)
            {
                if (string.IsNullOrEmpty(act))
                {
                    //No activator currently available, return unavailable
                    Logger.LogDebug("[Partition] No activator currently available");
                    return ActorPidResponse.Unavailable;
                }

                try
                {
                    return await Cluster.Remote.SpawnNamedAsync(activator, req.Name, req.Kind, Cluster.Config!.TimeoutTimespan);
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
}