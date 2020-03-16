// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron HB">
//       Copyright (C) 2015-2018 Asynkron HB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster
{
    class Partition
    {
        private readonly Dictionary<string, PID> KindMap = new Dictionary<string, PID>();

        private Subscription<object> memberStatusSub;
        private Cluster Cluster { get; }
        internal Partition(Cluster cluster)
        {
            Cluster = cluster;
        }

        public void Setup(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                KindMap[kind] = pid;
            }

            memberStatusSub = Cluster.System.EventStream.Subscribe<MemberStatusEvent>(
                msg =>
                {
                    foreach (var kind in msg.Kinds)
                    {
                        if (KindMap.TryGetValue(kind, out var kindPid))
                        {
                            Cluster.System.Root.Send(kindPid, msg);
                        }
                    }
                }
            );
        }

        private PID SpawnPartitionActor(string kind)
        {
            var props = Props.FromProducer(() => new PartitionActor(Cluster, kind)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = Cluster.System.Root.SpawnNamed(props, "partition-" + kind);
            return pid;
        }

        public void Stop()
        {
            foreach (var kind in KindMap.Values)
            {
                Cluster.System.Root.Stop(kind);
            }

            KindMap.Clear();
            Cluster.System.EventStream.Unsubscribe(memberStatusSub.Id);
        }

        public PID PartitionForKind(string address, string kind) => new PID(address, "partition-" + kind);
    }

    class PartitionActor : IActor
    {
        private static readonly ILogger _logger = Log.CreateLogger<PartitionActor>();

        private class SpawningProcess : TaskCompletionSource<ActorPidResponse>
        {
            public string SpawningAddress { get; }
            public SpawningProcess(string address) => SpawningAddress = address;
        }

        private readonly string _kind;
        private readonly Dictionary<string, PID> _partition = new Dictionary<string, PID>();        //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name
        private readonly Dictionary<string, SpawningProcess> _spawningProcs = new Dictionary<string, SpawningProcess>(); //spawning processes
        public Cluster Cluster
        {
            get;
        }
        public PartitionActor(Cluster cluster, string kind)
        {
            Cluster = cluster;
            _kind = kind;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PartitionActor for {Kind}", _kind);
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
                _partition.Remove(key);
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
                var owner = Cluster.Partition.PartitionForKind(address, _kind);

                context.Send(owner, msg);
            }
            else
            {
                _logger.LogDebug("Kind {Kind} Take Ownership name: {Name}, pid: {Pid}", _kind, msg.Name, msg.Pid);
                _partition[msg.Name] = msg.Pid;
                _reversePartition[msg.Pid] = msg.Name;
                context.Watch(msg.Pid);
            }
        }

        private void MemberLeft(MemberLeftEvent msg, IContext context)
        {
            _logger.LogInformation("Kind {Kind} member left {Address}", _kind, msg.Address);

            //If the left member is self, transfer remaining pids to others
            if (msg.Address == Cluster.System.ProcessRegistry.Address)
            {
                //TODO: right now we transfer ownership on a per actor basis.
                //this could be done in a batch
                //ownership is also racy, new nodes should maybe forward requests to neighbours (?)
                foreach (var (actorId, _) in _partition.ToArray())
                {
                    var address = Cluster.MemberList.GetPartition(actorId, _kind);

                    if (!string.IsNullOrEmpty(address))
                    {
                        TransferOwnership(actorId, address, context);
                    }
                }
            }

            foreach (var (actorId, pid) in _partition.ToArray())
            {
                if (pid.Address == msg.Address)
                {
                    _partition.Remove(actorId);
                    _reversePartition.Remove(pid);
                }
            }

            //Process Spawning Process
            foreach (var (_, sp) in _spawningProcs)
            {
                if (sp.SpawningAddress == msg.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void MemberRejoined(MemberRejoinedEvent msg)
        {
            _logger.LogInformation("Kind {Kind} member rejoined {Address}", _kind, msg.Address);

            foreach (var (actorId, pid) in _partition.ToArray())
            {
                if (pid.Address == msg.Address)
                {
                    _partition.Remove(actorId);
                    _reversePartition.Remove(pid);
                }
            }

            //Process Spawning Process
            foreach (var (_, sp) in _spawningProcs)
            {
                if (sp.SpawningAddress == msg.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void MemberJoined(MemberJoinedEvent msg, IContext context)
        {
            _logger.LogInformation("Kind {Kind} member joined {Address}", _kind, msg.Address);

            //TODO: right now we transfer ownership on a per actor basis.
            //this could be done in a batch
            //ownership is also racy, new nodes should maybe forward requests to neighbours (?)
            foreach (var (actorId, _) in _partition.ToArray())
            {
                var address = Cluster.MemberList.GetPartition(actorId, _kind);

                if (!string.IsNullOrEmpty(address) && address != Cluster.System.ProcessRegistry.Address)
                {
                    TransferOwnership(actorId, address, context);
                }
            }

            foreach (var (actorId, sp) in _spawningProcs)
            {
                var address = Cluster.MemberList.GetPartition(actorId, _kind);

                if (!string.IsNullOrEmpty(address) && address != Cluster.System.ProcessRegistry.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void TransferOwnership(string actorId, string address, IContext context)
        {
            var pid = _partition[actorId];
            var owner = Cluster.Partition.PartitionForKind(address, _kind);
            context.Send(owner, new TakeOwnership { Name = actorId, Pid = pid });
            _partition.Remove(actorId);
            _reversePartition.Remove(pid);
            context.Unwatch(pid);
        }

        private void Spawn(ActorPidRequest msg, IContext context)
        {
            //Check if exist in current partition dictionary
            if (_partition.TryGetValue(msg.Name, out var pid))
            {
                context.Respond(new ActorPidResponse { Pid = pid });
                return;
            }

            //Check if is spawning, if so just await spawning finish.
            if (_spawningProcs.TryGetValue(msg.Name, out var spawning))
            {
                context.ReenterAfter(
                    spawning.Task, rst =>
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
                _logger.LogDebug("No members currently available");
                context.Respond(ActorPidResponse.Unavailable);
                return;
            }

            //Create SpawningProcess and cache it in spawning dictionary.
            spawning = new SpawningProcess(activator);
            _spawningProcs[msg.Name] = spawning;

            //Await SpawningProcess
            context.ReenterAfter(
                spawning.Task, rst =>
                {
                    _spawningProcs.Remove(msg.Name);

                    //Check if exist in current partition dictionary
                    //This is necessary to avoid race condition during partition map transfering.
                    if (_partition.TryGetValue(msg.Name, out pid))
                    {
                        context.Respond(new ActorPidResponse { Pid = pid });
                        return Actor.Done;
                    }

                    //Check if process is faulted
                    if (rst.IsFaulted)
                    {
                        context.Respond(ActorPidResponse.Err);
                        return Actor.Done;
                    }

                    var pidResp = rst.Result;

                    if ((ResponseStatusCode)pidResp.StatusCode == ResponseStatusCode.OK)
                    {
                        pid = pidResp.Pid;
                        _partition[msg.Name] = pid;
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

            do
            {
                pidResp = await TrySpawn(retry == retryCount ? activator : Cluster.MemberList.GetActivator(req.Kind));
            } while ((ResponseStatusCode)pidResp.StatusCode == ResponseStatusCode.Unavailable && retry-- > 0);

            spawning.TrySetResult(pidResp);

            async Task<ActorPidResponse> TrySpawn(string act)
            {
                if (string.IsNullOrEmpty(act))
                {
                    //No activator currently available, return unavailable
                    _logger.LogDebug("No activator currently available");
                    return ActorPidResponse.Unavailable;
                }

                try
                {
                    return await Cluster.Remote.SpawnNamedAsync(activator, req.Name, req.Kind, Cluster.Config.TimeoutTimespan);
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