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

        public static PID SpawnPartitionActor(string kind)
        {
            var pid = Actor.SpawnNamed(Actor.FromProducer(() => new PartitionActor(kind)), "partition-" + kind);
            return pid;
        }

        public static void SubscribeToEventStream()
        {
            EventStream.Instance.Subscribe<MemberStatusEvent>(async msg =>
            {
                foreach (var kind in msg.Kinds)
                {
                    if (KindMap.TryGetValue(kind, out var kindPid))
                    {
                        await kindPid.SendAsync(msg);
                    }
                }
            });
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
    }

    internal class PartitionActor : IActor
    {
        private readonly string _kind;
        private readonly ILogger _logger = Log.CreateLogger<PartitionActor>();

        private readonly Dictionary<string, PID> _partition = new Dictionary<string, PID>(); //actor/grain name to PID

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
                case MemberJoinedEvent msg:
                    await MemberJoinedAsync(msg);
                    break;
                case MemberRejoinedEvent msg:
                    MemberRejoined(msg);
                    break;
                case MemberLeftEvent msg:
                    MemberLeft(msg);
                    break;
                case MemberAvailableEvent msg:
                    MemberAvailable(msg);
                    break;
                case MemberUnavailableEvent msg:
                    MemberUnavailable(msg);
                    break;
                case TakeOwnership msg:
                    TakeOwnership(msg);
                    break;
            }
        }

        private void TakeOwnership(TakeOwnership msg)
        {
            _partition[msg.Name] = msg.Pid;
        }

        private void MemberUnavailable(MemberUnavailableEvent msg)
        {
            _logger.LogInformation("Member Unavailable {0}", msg.Address);
        }

        private void MemberAvailable(MemberAvailableEvent msg)
        {
            _logger.LogInformation("Member Available {0}", msg.Address);
        }

        private void MemberLeft(MemberLeftEvent msg)
        {
            _logger.LogInformation("Member Left {0}", msg.Address);
            foreach (var (actorId, pid) in _partition.ToArray())
            {
                if (pid.Address == msg.Address)
                {
                    _partition.Remove(actorId);
                }
            }
        }

        private void MemberRejoined(MemberRejoinedEvent msg)
        {
            _logger.LogInformation("Member Rejoined {0}", msg.Address);

            foreach (var (actorId, pid) in _partition.ToArray())
            {
                if (pid.Address == msg.Address)
                {
                    _partition.Remove(actorId);
                }
            }
        }

        private async Task MemberJoinedAsync(MemberJoinedEvent msg)
        {
            _logger.LogInformation("Member Joined {0}", msg.Address);
            //TODO: right now we transfer ownership on a per actor basis.
            //this could be done in a batch
            //ownership is also racy, new nodes should maybe forward requests to neighbours (?)
            foreach (var (actorId, _) in _partition.ToArray())
            {
                var address = await MemberList.GetMemberAsync(actorId, _kind);

                if (address != ProcessRegistry.Instance.Address)
                {
                    await TransferOwnership(actorId, address);
                }
            }
        }

        private async Task TransferOwnership(string actorId, string address)
        {
            var pid = _partition[actorId];
            var owner = Partition.PartitionForKind(address, _kind);
            await owner.SendAsync(new TakeOwnership
                       {
                           Name = actorId,
                           Pid = pid
                       });
            _partition.Remove(actorId);
        }

        private async Task Spawn(ActorPidRequest msg, IContext context)
        {
            PID pid;
            if (!_partition.TryGetValue(msg.Name, out pid))
            {
                var random = await MemberList.GetRandomActivatorAsync(msg.Kind);
                pid = await Remote.Remote.SpawnNamedAsync(random, msg.Name, msg.Kind, TimeSpan.FromSeconds(5));
                _partition[msg.Name] = pid;
            }
            await context.RespondAsync(new ActorPidResponse {Pid = pid});
        }
    }
}