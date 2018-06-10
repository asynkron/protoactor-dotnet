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
    internal static class Partition
    {
        public static Dictionary<string, PID> KindMap = new Dictionary<string, PID>();

        private static Subscription<object> _memberStatusSub;

        public static void Setup(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                KindMap[kind] = pid;
            }

            _memberStatusSub = EventStream.Instance.Subscribe<MemberStatusEvent>(msg =>
            {
                foreach (var kind in msg.Kinds)
                {
                    if (KindMap.TryGetValue(kind, out var kindPid))
                    {
                        RootContext.Empty.Send(kindPid, msg);
                    }
                }
            });
        }

        public static PID SpawnPartitionActor(string kind)
        {
            var props = Props.FromProducer(() => new PartitionActor(kind)).WithGuardianSupervisorStrategy(Supervision.AlwaysRestartStrategy);
            var pid = RootContext.Empty.SpawnNamed(props, "partition-" + kind);
            return pid;
        }

        public static void Stop()
        {
            foreach (var kind in KindMap.Values)
            {
                kind.Stop();
            }
            KindMap.Clear();
            EventStream.Instance.Unsubscribe(_memberStatusSub.Id);
        }

        public static PID PartitionForKind(string address, string kind)
        {
            return new PID(address, "partition-" + kind);
        }
    }

    internal class PartitionActor : IActor
    {
        private class SpawningProcess : TaskCompletionSource<ActorPidResponse>
        {
            public string SpawningAddress { get; }
            public SpawningProcess(string address) => SpawningAddress = address;
        }

        private readonly string _kind;
        private readonly ILogger _logger = Log.CreateLogger<PartitionActor>();

        private readonly Dictionary<string, PID> _partition = new Dictionary<string, PID>(); //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name

        private readonly Dictionary<string, SpawningProcess> _spawningProcs = new Dictionary<string, SpawningProcess>(); //spawning processes

        public PartitionActor(string kind)
        {
            _kind = kind;
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PartitionActor " + _kind);
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
            var address = MemberList.GetPartition(msg.Name, _kind);
            if (!string.IsNullOrEmpty(address) && address != ProcessRegistry.Instance.Address)
            {
                //if not, forward to the correct owner
                var owner = Partition.PartitionForKind(address, _kind);
                
                context.Send(owner, msg);
            }
            else
            {
                _logger.LogDebug($"Kind {_kind} Take Ownership name: {msg.Name}, pid: {msg.Pid}");
                _partition[msg.Name] = msg.Pid;
                _reversePartition[msg.Pid] = msg.Name;
                context.Watch(msg.Pid);
            }
        }

        private void MemberLeft(MemberLeftEvent msg, IContext context)
        {
            _logger.LogInformation($"Kind {_kind} Member Left {msg.Address}");
            //If the left member is self, transfer remaining pids to others
            if (msg.Address == ProcessRegistry.Instance.Address)
            {
                //TODO: right now we transfer ownership on a per actor basis.
                //this could be done in a batch
                //ownership is also racy, new nodes should maybe forward requests to neighbours (?)
                foreach (var (actorId, _) in _partition.ToArray())
                {
                    var address = MemberList.GetPartition(actorId, _kind);

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
            _logger.LogInformation($"Kind {_kind} Member Rejoined {msg.Address}");

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
            _logger.LogInformation($"Kind {_kind} Member Joined {msg.Address}");
            //TODO: right now we transfer ownership on a per actor basis.
            //this could be done in a batch
            //ownership is also racy, new nodes should maybe forward requests to neighbours (?)
            foreach (var (actorId, _) in _partition.ToArray())
            {
                var address = MemberList.GetPartition(actorId, _kind);

                if (!string.IsNullOrEmpty(address) && address != ProcessRegistry.Instance.Address)
                {
                    TransferOwnership(actorId, address, context);
                }
            }

            foreach (var (actorId, sp) in _spawningProcs)
            {
                var address = MemberList.GetPartition(actorId, _kind);

                if (!string.IsNullOrEmpty(address) && address != ProcessRegistry.Instance.Address)
                {
                    sp.TrySetResult(ActorPidResponse.Unavailable);
                }
            }
        }

        private void TransferOwnership(string actorId, string address, IContext context)
        {
            var pid = _partition[actorId];
            var owner = Partition.PartitionForKind(address, _kind);
            context.Send(owner, new TakeOwnership
                       {
                           Name = actorId,
                           Pid = pid
                       });
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
            SpawningProcess spawning;
            if (_spawningProcs.TryGetValue(msg.Name, out spawning))
            {
                context.ReenterAfter(spawning.Task, rst =>
                {
                    context.Respond(rst.IsFaulted ? ActorPidResponse.Err : rst.Result);
                    return Actor.Done;
                });
                return;
            }

            //Get activator
            var activator = MemberList.GetActivator(msg.Kind);
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
            context.ReenterAfter(spawning.Task, rst =>
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
            });

            //Perform Spawning
            Task.Factory.StartNew(() => Spawning(msg, activator, 3, spawning));
        }

        private async Task Spawning(ActorPidRequest req, string activator, int retryLeft, SpawningProcess spawning)
        {
            if (string.IsNullOrEmpty(activator))
            {
                activator = MemberList.GetActivator(req.Kind);
                if (string.IsNullOrEmpty(activator))
                {
                    //No activator currently available, return unavailable
                    _logger.LogDebug("No activator currently available");
                    spawning.TrySetResult(ActorPidResponse.Unavailable);
                    return;
                }
            }

            ActorPidResponse pidResp;
            try
            {
                pidResp = await Remote.Remote.SpawnNamedAsync(activator, req.Name, req.Kind, Cluster.Config.TimeoutTimespan);
            }
            catch (TimeoutException)
            {
                spawning.TrySetResult(ActorPidResponse.TimeOut);
                return;
            }
            catch
            {
                spawning.TrySetResult(ActorPidResponse.Err);
                return;
            }

            if ((ResponseStatusCode)pidResp.StatusCode == ResponseStatusCode.Unavailable && retryLeft != 0)
            {
                await Spawning(req, null, --retryLeft, spawning);
                return;
            }

            spawning.TrySetResult(pidResp);
        }
    }
}