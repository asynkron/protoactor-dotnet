// -----------------------------------------------------------------------
//   <copyright file="Activator.cs" company="Asynkron AB">
//       Copyright (C) 2015-2020 Asynkron AB All rights reserved
//   </copyright>
// -----------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    internal class PartitionPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        private readonly Dictionary<string, (PID pid, string kind)> _myActors =
            new Dictionary<string, (PID pid, string kind)>();

        private readonly PartitionManager _partitionManager;
        private readonly Rendezvous _rdv = new Rendezvous();

        private readonly Remote.Remote _remote;
        private readonly ActorSystem _system;
        private ulong _eventId;

        public PartitionPlacementActor(Cluster cluster, PartitionManager partitionManager)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
            _partitionManager = partitionManager;
            _logger = Log.CreateLogger($"{nameof(PartitionPlacementActor)}-{cluster.LoggerId}");
            _system.EventStream.Subscribe<DeadLetterEvent>(dl =>
                {
                    if (dl.Pid.Id.StartsWith(PartitionManager.PartitionPlacementActorName))
                    {
                        var kvp = _myActors.FirstOrDefault(kvp => kvp.Value.pid.Equals(dl.Pid));

                        if (kvp.Equals(default))
                        {
                            return;
                        }

                        var id = kvp.Key;

                        if (dl.Sender != null)
                        {
                            _system.Root.Send(dl.Sender, new VoidResponse());
                            _logger.LogWarning(
                                "Got Deadletter message {Message} for gain actor '{Identity}' from {Sender}, sending void response",
                                dl.Message, id, dl.Sender
                            );
                        }
                        else
                        {
                            _logger.LogWarning(
                                "Got Deadletter message {Message} for gain actor '{Identity}', use `Request` for grain communication ",
                                dl.Message, id
                            );
                        }
                    }
                }
            );
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started _                   => Actor.Done, //  context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
                ReceiveTimeout _            => ReceiveTimeout(context),
                Terminated msg              => HandleTerminated(context, msg),
                ClusterTopology msg         => HandleClusterTopology(msg),
                IdentityHandoverRequest msg => HandleIdentityHandoverRequest(context, msg),
                ActivationRequest msg       => HandleActivationRequest(context, msg),
                _                           => Actor.Done
            };

        private Task ReceiveTimeout(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            _logger.LogInformation("I am idle");
            return Actor.Done;
        }

        private Task HandleTerminated(IContext context, Terminated msg)
        {
            //TODO: this can be done better
            var (identity, (pid, kind)) = _myActors.FirstOrDefault(kvp => kvp.Value.pid.Equals(msg.Who));

            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                Kind = kind,
                EventId = _eventId,
                Identity = identity
            };

            var ownerAddress = _rdv.GetOwnerMemberByIdentity(identity);
            var ownerPid = _partitionManager.RemotePartitionIdentityActor(ownerAddress);

            context.Send(ownerPid, activationTerminated);
            return Actor.Done;
        }

        private Task HandleClusterTopology(ClusterTopology msg)
        {
            if (msg.EventId > _eventId)
            {
                _eventId = msg.EventId;
                var members = msg.Members.ToArray();
                _rdv.UpdateMembers(members);
                _eventId = msg.EventId;
            }

            return Actor.Done;
        }

        //this is pure, we do not change any state or actually move anything
        //the requester also provide its own view of the world in terms of members
        private Task HandleIdentityHandoverRequest(IContext context, IdentityHandoverRequest msg)
        {
            var count = 0;
            var response = new IdentityHandoverResponse();
            var requestAddress = context.Sender.Address;

            var rdv = new Rendezvous();
            rdv.UpdateMembers(msg.Members);
            //  _logger.LogDebug("Handling IdentityHandoverRequest - request from " + requestAddress);
            //  _logger.LogDebug(msg.Members.ToLogString());

            foreach (var (identity, (pid, kind)) in _myActors.ToArray())
            {
                var ownerAddress = rdv.GetOwnerMemberByIdentity(identity);

                if (ownerAddress != requestAddress)
                {
                    //       _logger.LogInformation("{Identity} belongs to {OwnerAddress} - {EventId}", identity, ownerAddress, msg.EventId);
                    continue;
                }

                _logger.LogDebug("TRANSFER {Identity} TO {newOwnerAddress} -- {EventId}", identity, ownerAddress,
                    msg.EventId
                );
                var actor = new Activation {Identity = identity, Kind = kind, Pid = pid, EventId = msg.EventId};
                response.Actors.Add(actor);
                count++;
            }


            //always respond, this is request response msg
            context.Respond(response);

            _logger.LogDebug("Transferred {Count} actor ownership to other members", count);
            return Actor.Done;
        }

        private Task HandleActivationRequest(IContext context, ActivationRequest msg)
        {
            var props = _remote.GetKnownKind(msg.Kind);
            var identity = msg.Identity;
            if (string.IsNullOrEmpty(identity))
            {
                identity = _system.ProcessRegistry.NextId();
            }

            try
            {
                //spawn and remember this actor
                var pid = context.SpawnPrefix(props, identity);
                _myActors[identity] = (pid, msg.Kind);

                var response = new ActivationResponse
                {
                    Pid = pid
                };
                context.Respond(response);
            }
            catch (ProcessNameExistException ex)
            {
                var response = new ActivationResponse
                {
                    Pid = ex.Pid
                };
                context.Respond(response);
            }
            catch
            {
                var response = new ActivationResponse
                {
                    Pid = null
                };
                context.Respond(response);

                throw;
            }

            return Actor.Done;
        }
    }
}