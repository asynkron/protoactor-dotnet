// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    internal class PartitionPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        private readonly Dictionary<string, (PID pid, string kind, string identityOwner)> _myActors =
            new Dictionary<string, (PID pid, string kind, string identityOwner)>();
        
        private readonly Remote.Remote _remote;
        private readonly ActorSystem _system;
        private ulong _eventId;
        private readonly Rendezvous _rdv = new Rendezvous();

        public PartitionPlacementActor(Cluster cluster)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
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
        
        private void SendLater(object msg, IContext context)
        {
            var self = context.Self;
            var sender = context.Sender;
            Task.Delay(100).ContinueWith(t => context.Request(self, msg,sender));
        }

        public Task ReceiveAsync(IContext context)
        {
            switch (context.Message)
            {
                case IdentityHandoverRequest msg:

                    //if we are not up to date with the topology event as the requester.
                    //try again once we are
                    if (msg.EventId > _eventId)
                    {
                        SendLater(msg,context);
                        return Actor.Done;
                    }

                    //the other node is dated, respond empty message
                    if (msg.EventId < _eventId)
                    {
                        context.Respond(new IdentityHandoverResponse());
                        return Actor.Done;
                    }

                    //this node is in sync with the requester, go ahead and transfer
                    HandleOwnershipTransfer(msg, context);
                    break;
                case ClusterTopology msg:
                    //only handle newer events
                    if (_eventId < msg.EventId)
                    {
                        _eventId = msg.EventId;
                        _rdv.UpdateMembers(msg.Members);
                    }
                    break;
                case ActorPidRequest msg:
                    HandleActorPidRequest(context, msg);
                    break;
            }

            return Actor.Done;
        }

        private void HandleOwnershipTransfer(IdentityHandoverRequest msg, IContext context)
        {
            var count = 0;
            var response = new IdentityHandoverResponse();
            _logger.LogDebug("Handling IdentityHandoverRequest");
            
            foreach (var (identity, (pid, kind, oldOwnerAddress)) in _myActors.ToArray())
            {
                var ownerAddress = _rdv.GetOwnerMemberByIdentity(identity);

                if (ownerAddress != msg.Address)
                {
                    continue;
                }

                _logger.LogDebug("TRANSFER {pid} FROM {oldOwnerAddress} TO {newOwnerAddress} -- {EventId}", pid, oldOwnerAddress,
                    ownerAddress, _eventId
                );
                var actor = new TakeOwnership {Name = identity, Kind = kind, Pid = pid, EventId = _eventId};
                response.Actors.Add(actor);
                _myActors[identity] = (pid, kind, ownerAddress);
                count++;
            }
            
            _logger.LogDebug("Responding to IdentityHandoverRequest");
            //always respond, this is request response msg
            context.Respond(response);

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