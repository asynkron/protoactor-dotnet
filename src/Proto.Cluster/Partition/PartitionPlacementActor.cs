// -----------------------------------------------------------------------
// <copyright file="PartitionPlacementActor.cs" company="Asynkron AB">
//      Copyright (C) 2015-2020 Asynkron AB All rights reserved
// </copyright>
// -----------------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Proto.Cluster.Partition
{
    class PartitionPlacementActor : IActor
    {
        private readonly Cluster _cluster;
        private readonly ILogger _logger;

        //pid -> the actor that we have created here
        //kind -> the actor kind
        private readonly Dictionary<ClusterIdentity, PID> _myActors = new();

        public PartitionPlacementActor(Cluster cluster)
        {
            _cluster = cluster;
            _logger = Log.CreateLogger($"{nameof(PartitionPlacementActor)}-{cluster.LoggerId}");
        }

        public Task ReceiveAsync(IContext context) =>
            context.Message switch
            {
                Terminated msg              => Terminated(context, msg),
                IdentityHandoverRequest msg => IdentityHandoverRequest(context, msg),
                ActivationRequest msg       => ActivationRequest(context, msg),
                _                           => Task.CompletedTask
            };

        private Task Terminated(IContext context, Terminated msg)
        {
            //TODO: if this turns out to be perf intensive, lets look at optimizations for reverse lookups
            var (clusterIdentity, pid) = _myActors.FirstOrDefault(kvp => kvp.Value.Equals(msg.Who));

            var activationTerminated = new ActivationTerminated
            {
                Pid = pid,
                ClusterIdentity = clusterIdentity,
            };

            _cluster.MemberList.BroadcastEvent(activationTerminated);

            // var ownerAddress = _rdv.GetOwnerMemberByIdentity(clusterIdentity.Identity);
            // var ownerPid = PartitionManager.RemotePartitionIdentityActor(ownerAddress);
            //
            // context.Send(ownerPid, activationTerminated);
            _myActors.Remove(clusterIdentity);
            return Task.CompletedTask;
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

            foreach (var (clusterIdentity, pid) in _myActors)
            {
                //who owns this identity according to the requesters memberlist?
                var ownerAddress = rdv.GetOwnerMemberByIdentity(clusterIdentity.Identity);

                //this identity is not owned by the requester
                if (ownerAddress != requestAddress) continue;

                _logger.LogDebug("Transfer {Identity} to {newOwnerAddress} -- {TopologyHash}", clusterIdentity, ownerAddress,
                    msg.TopologyHash
                );

                var actor = new Activation {ClusterIdentity = clusterIdentity, Pid = pid};
                response.Actors.Add(actor);
                count++;
            }

            //always respond, this is request response msg
            context.Respond(response);

            _logger.LogDebug("Transferred {Count} actor ownership to other members", count);
            return Task.CompletedTask;
        }

        private Task ActivationRequest(IContext context, ActivationRequest msg)
        {
            try
            {
                if (_myActors.TryGetValue(msg.ClusterIdentity, out var existing))
                {
                    //this identity already exists
                    var response = new ActivationResponse
                    {
                        Pid = existing
                    };
                    context.Respond(response);
                }
                else
                {
                    var clusterKind = _cluster.GetClusterKind(msg.ClusterIdentity.Kind);
                    //this actor did not exist, lets spawn a new activation

                    //spawn and remember this actor
                    //as this id is unique for this activation (id+counter)
                    //we cannot get ProcessNameAlreadyExists exception here

                    var clusterProps = clusterKind.Props.WithClusterIdentity(msg.ClusterIdentity);

                    var pid = context.SpawnPrefix(clusterProps, msg.ClusterIdentity.Identity);

                    _myActors[msg.ClusterIdentity] = pid;

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

            return Task.CompletedTask;
        }
    }
}