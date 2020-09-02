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
using Proto.Remote;

namespace Proto.Cluster.Partition
{
    internal class PartitionPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        //pid -> the actor that we have created here
        //kind -> the actor kind
        //eventId -> the cluster wide eventId when this actor was created
        private readonly Dictionary<string, (PID pid, string kind, ulong eventId)> _myActors =
            new Dictionary<string, (PID pid, string kind, ulong eventId)>();

        private readonly PartitionManager _partitionManager;
        private readonly Rendezvous _rdv = new Rendezvous();

        private readonly IRemote _remote;
        private readonly ActorSystem _system;
        
        //cluster wide eventId.
        //this is useful for knowing if we are in sync with, ahead of or behind other nodes requests
        private ulong _eventId; 

        public PartitionPlacementActor(Cluster cluster, PartitionManager partitionManager)
        {
            _cluster = cluster;
            _remote = _cluster.Remote;
            _system = _cluster.System;
            _partitionManager = partitionManager;
            _logger = Log.CreateLogger($"{nameof(PartitionPlacementActor)}-{cluster.LoggerId}");
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Started _                   => Started(context),
                ReceiveTimeout _            => ReceiveTimeout(context),
                Terminated msg              => Terminated(context, msg),
                ClusterTopology msg         => ClusterTopology(msg),
                IdentityHandoverRequest msg => IdentityHandoverRequest(context, msg),
                ActivationRequest msg       => ActivationRequest(context, msg),
                _                           => Actor.Done
            };

        private Task Started(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            return Actor.Done;
        }

        private Task ReceiveTimeout(IContext context)
        {
            context.SetReceiveTimeout(TimeSpan.FromSeconds(5));
            var count = _myActors.Count;
            _logger.LogInformation("Statistics: Actor Count {ActorCount}", count);
            return Actor.Done;
        }

        private Task Terminated(IContext context, Terminated msg)
        {
            //TODO: if this turns out to be perf intensive, lets look at optimizations for reverse lookups
            var (identity, (pid, kind, eventId)) = _myActors.FirstOrDefault(kvp => kvp.Value.pid.Equals(msg.Who));

            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                Kind = kind,
                EventId = eventId,
                Identity = identity
            };

            var ownerAddress = _rdv.GetOwnerMemberByIdentity(identity);
            var ownerPid = _partitionManager.RemotePartitionIdentityActor(ownerAddress);

            context.Send(ownerPid, activationTerminated);
            _myActors.Remove(identity);
            return Actor.Done;
        }

        private Task ClusterTopology(ClusterTopology msg)
        {
            //ignore outdated events
            if (msg.EventId <= _eventId)
            {
                return Actor.Done;
            }

            _eventId = msg.EventId;
            _rdv.UpdateMembers(msg.Members);

            return Actor.Done;
        }

        //this is pure, we do not change any state or actually move anything
        //the requester also provide its own view of the world in terms of members
        //TLDR; we are not using any topology state from this actor itself
        private Task IdentityHandoverRequest(IContext context, IdentityHandoverRequest msg)
        {
            var count = 0;
            var response = new IdentityHandoverResponse();
            var requestAddress = context.Sender!.Address;

            //use a local selector, which is based on the requesters view of the world
            var rdv = new Rendezvous();
            rdv.UpdateMembers(msg.Members);
            
            foreach (var (identity, (pid, kind, eventId)) in _myActors)
            {
                //who owns this identity according to the requesters memberlist?
                var ownerAddress = rdv.GetOwnerMemberByIdentity(identity);

                //this identity is not owned by the requester
                if (ownerAddress != requestAddress)
                {
                    continue;
                }

                _logger.LogDebug("Transfer {Identity} to {newOwnerAddress} -- {EventId}", identity, ownerAddress,
                    msg.EventId
                );
                
                var actor = new Activation {Identity = identity, Kind = kind, Pid = pid, EventId = eventId};
                response.Actors.Add(actor);
                count++;
            }


            //always respond, this is request response msg
            context.Respond(response);

            _logger.LogDebug("Transferred {Count} actor ownership to other members", count);
            return Actor.Done;
        }

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            var props = _remote.RemoteKindRegistry.GetKnownKind(msg.Kind);
            var identity = msg.Identity;

            try
            {
                if (_myActors.TryGetValue(identity, out var existing))
                {
                    //TODO: should we use identity+kind as key?

                    //this identity already exists
                    var response = new ActivationResponse
                    {
                        Pid = existing.pid
                    };
                    context.Respond(response);
                }
                else
                {
                    //this actor did not exist, lets spawn a new activation
                    
                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here
                    var pid = context.SpawnPrefix(props, identity);
                    _myActors[identity] = (pid, msg.Kind, _eventId);

                    var response = new ActivationResponse
                    {
                        Pid = pid
                    };
                    context.Respond(response);
                }
            }
            catch
            {
                var response = new ActivationResponse
                {
                    Pid = null
                };
                context.Respond(response);
            }

            return Actor.Done;
        }
    }
}