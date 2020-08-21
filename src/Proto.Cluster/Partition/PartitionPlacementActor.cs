// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Cluster.Events;
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    internal class PartitionPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        private readonly Dictionary<string, (PID pid, string kind, string identityOwner)> _myActors =
            new Dictionary<string, (PID pid, string kind, string identityOwner)>();

        private readonly PartitionManager _partitionManager;
        private readonly Remote.Remote _remote;
        private readonly ActorSystem _system;

        public PartitionPlacementActor(Cluster cluster, PartitionManager partitionManager)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
            _partitionManager = partitionManager;
            _logger = Log.CreateLogger($"{nameof(PartitionPlacementActor)}-{cluster.Id}");
            _system.EventStream.Subscribe<DeadLetterEvent>(dl =>
                {
                    if (dl.Pid.Id.StartsWith(PartitionManager.PartitionPlacementActorName))
                    {
                        var id = dl.Pid.Id.Substring(PartitionManager.PartitionPlacementActorName.Length + 1);

                        if (dl.Message is Watch watch)
                        {
                            //we got a deadletter watch, reply with a terminated event
                            _system.Root.Send(watch.Watcher, new Terminated
                            {
                                AddressTerminated = false,
                                Who = dl.Pid
                            });
                        }
                        else if (dl.Sender != null)
                        {
                            _system.Root.Send(dl.Sender, new VoidResponse());
                            _logger.LogWarning("Got Deadletter message {Message} for gain actor '{Identity}' from {Sender}, sending void response", dl.Message, id,dl.Sender);
                        }
                        else
                        {
                            _logger.LogWarning("Got Deadletter message {Message} for gain actor '{Identity}', use `Request` for grain communication ", dl.Message, id);    
                        }
                    }
                }
            );
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case MemberLeftEvent _:
                case MemberJoinedEvent _:
                    //TODO: check what needs to be transferred
                    HandleOwnershipTransfer(context);

                    break;
                case ActorPidRequest msg:
                    HandleActorPidRequest(context, msg);
                    break;
            }

            return Actor.Done;
        }

        private void HandleOwnershipTransfer(IContext context)
        {
            var count = 0;
            foreach (var (identity, (pid, kind, oldOwnerAddress)) in _myActors.ToArray())
            {
                var newOwnerAddress = _partitionManager.Selector.GetIdentityOwner(identity);
                if (newOwnerAddress != oldOwnerAddress)
                {
                    _logger.LogDebug("TRANSFER {pid} FROM {oldOwnerAddress} TO {newOwnerAddress}", pid, oldOwnerAddress,
                        newOwnerAddress
                    );
                    var owner = _partitionManager.RemotePartitionIdentityActor(newOwnerAddress);
                    context.Send(owner, new TakeOwnership {Name = identity, Kind = kind, Pid = pid});
                    _myActors[identity] = (pid, kind, newOwnerAddress);
                    count++;
                }
            }

            if (count > 0)
            {
                _logger.LogDebug("Transferred {Count} actor ownership to other members", count);
            }
        }

        private void HandleActorPidRequest(IContext context, ActorPidRequest msg)
        {
            var props = _remote.GetKnownKind(msg.Kind);
            var name = msg.Name;
            if (string.IsNullOrEmpty(name))
            {
                name = _system.ProcessRegistry.NextId();
            }

            try
            {
                //spawn and remember this actor
                var pid = context.SpawnNamed(props, name);
                _myActors[name] = (pid, msg.Kind, context.Sender!.Address);

                var response = new ActorPidResponse {Pid = pid};
                context.Respond(response);
            }
            catch (ProcessNameExistException ex)
            {
                var response = new ActorPidResponse
                {
                    Pid = ex.Pid,
                    StatusCode = (int) ResponseStatusCode.ProcessNameAlreadyExist
                };
                context.Respond(response);
            }
            catch
            {
                var response = new ActorPidResponse
                {
                    StatusCode = (int) ResponseStatusCode.Error
                };
                context.Respond(response);

                throw;
            }
        }
    }
}