// -----------------------------------------------------------------------
//   <copyright file="Partition.cs" company="Asynkron HB">
//       Copyright (C) 2015-2017 Asynkron HB All rights reserved
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

        private static Subscription<object> memberStatusSub;

        public static PID SpawnPartitionActor(string kind)
        {
            var pid = Actor.SpawnNamed(Actor.FromProducer(() => new PartitionActor(kind)), "partition-" + kind);
            return pid;
        }

        public static void SubscribeToEventStream()
        {
            memberStatusSub = EventStream.Instance.Subscribe<MemberStatusEvent>(msg =>
            {
                foreach (var kind in msg.Kinds)
                {
                    if (KindMap.TryGetValue(kind, out var kindPid))
                    {
                        kindPid.Tell(msg);
                    }
                }
            });
        }

        public static void UnsubEventStream()
        {
            EventStream.Instance.Unsubscribe(memberStatusSub.Id);
        }

        public static PID PartitionForKind(string address, string kind)
        {
            return new PID(address, "partition-" + kind);
        }

        public static void SpawnPartitionActors(string[] kinds)
        {
            foreach (var kind in kinds)
            {
                var pid = SpawnPartitionActor(kind);
                KindMap[kind] = pid;
            }
        }

        public static void StopPartitionActors()
        {
            foreach (var kind in KindMap.Values)
            {
                kind.Stop();
            }
            KindMap.Clear();
        }
    }

    internal class PartitionActor : IActor
    {
        private readonly string _kind;
        private readonly ILogger _logger = Log.CreateLogger<PartitionActor>();

        private readonly Dictionary<string, PID> _partition = new Dictionary<string, PID>(); //actor/grain name to PID
        private readonly Dictionary<PID, string> _reversePartition = new Dictionary<PID, string>(); //PID to grain name

        public PartitionActor(string kind)
        {
            _kind = kind;
        }

        public async Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case Started _:
                    _logger.LogDebug("Started PartitionActor " + _kind);
                    break;
                case ActorPidRequest msg:
                    await Spawn(msg, context);
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
            _logger.LogDebug($"Kind {_kind} Take Ownership name: {msg.Name}, pid: {msg.Pid}");
            _partition[msg.Name] = msg.Pid;
            _reversePartition[msg.Pid] = msg.Name;
            context.Watch(msg.Pid);
        }

        private void MemberLeft(MemberLeftEvent msg, IContext context)
        {
            _logger.LogInformation($"Kind {_kind} Member Left {msg.Address}");
            foreach (var (actorId, pid) in _partition.ToArray())
            {
                if (pid.Address == msg.Address)
                {
                    _partition.Remove(actorId);
                    _reversePartition.Remove(pid);
                }
            }

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
        }

        private void TransferOwnership(string actorId, string address, IContext context)
        {
            var pid = _partition[actorId];
            var owner = Partition.PartitionForKind(address, _kind);
            owner.Tell(new TakeOwnership
                       {
                           Name = actorId,
                           Pid = pid
                       });
            _partition.Remove(actorId);
            _reversePartition.Remove(pid);
            context.Unwatch(pid);
        }

        private async Task Spawn(ActorPidRequest msg, IContext context)
        {
            if (_partition.TryGetValue(msg.Name, out var pid))
            {
                context.Respond(new ActorPidResponse {Pid = pid});
                return;
            }

            var activator = MemberList.GetActivator(msg.Kind);
            if (string.IsNullOrEmpty(activator))
            {
                //No activator currently available, return unavailable
                _logger.LogDebug("No members currently available");
                context.Respond(new ActorPidResponse {StatusCode = (int) ResponseStatusCode.Unavailable});
                return;
            }

            for (var retry = 3; retry >= 0; retry--)
            {
                if (string.IsNullOrEmpty(activator))
                {
                    activator = MemberList.GetActivator(msg.Kind);
                    if (string.IsNullOrEmpty(activator))
                    {
                        //No activator currently available, return unavailable
                        _logger.LogDebug("No activator currently available");
                        context.Respond(new ActorPidResponse {StatusCode = (int) ResponseStatusCode.Unavailable});
                        return;
                    }
                }

                ActorPidResponse pidResp;
                try
                {
                    pidResp = await Remote.Remote.SpawnNamedAsync(activator, msg.Name, msg.Kind, Cluster.cfg.TimeoutTimespan);
                }
                catch (TimeoutException)
                {
                    context.Respond(new ActorPidResponse {StatusCode = (int) ResponseStatusCode.Timeout});
                    throw;
                }
                catch
                {
                    context.Respond(new ActorPidResponse {StatusCode = (int) ResponseStatusCode.Error});
                    throw;
                }

                switch ((ResponseStatusCode) pidResp.StatusCode)
                {
                    case ResponseStatusCode.OK:
                        pid = pidResp.Pid;
                        _partition[msg.Name] = pid;
                        _reversePartition[pid] = msg.Name;
                        context.Watch(pid);
                        context.Respond(pidResp);
                        return;
                    case ResponseStatusCode.Unavailable:
                        //Get next activator to spawn
                        if (retry != 0)
                        {
                            activator = null;
                            continue;
                        }
                        context.Respond(pidResp);
                        return;
                    default:
                        //Return to requester to wait
                        context.Respond(pidResp);
                        return;
                }
            }
        }
    }
}